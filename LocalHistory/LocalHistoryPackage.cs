/*
Copyright 2013 Intel Corporation

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

   http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Intel.LocalHistory
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // This attribute registers a tool window exposed by this package.
    [ProvideToolWindow(typeof(LocalHistoryToolWindow))]
    [Guid(GuidList.guidLocalHistoryPkgString)]
    [ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82")]
    public sealed class LocalHistoryPackage : Package, IVsSolutionEvents, IVsSelectionEvents
    {
        private DTE dte;
        private uint solutionCookie;
        private uint rdtCookie;
        private uint selectionCookie;
        private DocumentRepository documentRepository;
        private LocalHistoryDocumentListener documentListener;

        private ToolWindowPane toolWindow;

        public LocalHistoryPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", ToString()));
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", ToString()));
            base.Initialize();

            var solution = (IVsSolution)GetService(typeof(SVsSolution));
            ErrorHandler.ThrowOnFailure(solution.AdviseSolutionEvents(this, out solutionCookie));

            // Add our command handlers for menu (commands must exist in the .vsct file)
            var mcs = (OleMenuCommandService)GetService(typeof(IMenuCommandService));
            if (null != mcs)
            {
                // Create the command for the menu item.
                var menuCommandID = new CommandID(GuidList.guidLocalHistoryCmdSet, (int)PkgCmdIDList.cmdidLocalHistoryMenuItem);
                var menuItem = new MenuCommand(ProjectItemContextMenuHandler, menuCommandID);
                mcs.AddCommand(menuItem);

                // Create the command for the tool window
                var toolwndCommandID = new CommandID(GuidList.guidLocalHistoryCmdSet, (int)PkgCmdIDList.cmdidLocalHistoryWindow);
                var menuToolWin = new MenuCommand(ToolWindowMenuItemHandler, toolwndCommandID);
                mcs.AddCommand(menuToolWin);
            }
        }

        #endregion

        /// <summary>
        /// This function is called when the user clicks the menu item that shows the 
        /// tool window. See the Initialize method to see how the menu item is associated to 
        /// this function using the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void ToolWindowMenuItemHandler(object sender, EventArgs e)
        {
            ShowToolWindow();
        }

        /// <summary>
        /// When a solution is opened, this function creates a new <code>DocumentRepository</code> and 
        /// registers the <code>LocalHistoryDocumentListener</code> to listen for save events.
        /// </summary>
        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering OnAfterOpenSolution() of: {0}", ToString()));

            dte = (DTE)GetService(typeof(DTE));
            if (dte == null)
            {
                ErrorHandler.ThrowOnFailure(1);
            }

            // The solution name can be empty if the user opens a file without opening a solution
            if (dte.Solution != null && dte.Solution.FullName.Length != 0)
            {
                RegisterDocumentListener();

                RegisterSelectionListener();
            }

            // Get the instance of the ToolWindow if there is one
            toolWindow = FindToolWindow(typeof(LocalHistoryToolWindow), 0, false);

            if (toolWindow != null)
            {
                // TODO: remove this
                // BUG: This will cause a null pointer exception if no solution is open when the user opens the tool window.
                documentRepository.Control = (LocalHistoryControl)toolWindow.Content;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// When a solution is closed, this function creates unsubscribed to documents and selection events.
        /// </summary>
        public int OnAfterCloseSolution(
            object pUnkReserved)
        {
            UnregisterDocumentListener();

            UnregisterSelectionListener();

            ClearToolWindow();

            return VSConstants.S_OK;
        }

        public void RegisterDocumentListener()
        {
            var documentTable = (IVsRunningDocumentTable)GetGlobalService(typeof(SVsRunningDocumentTable));

            Debug.WriteLine(dte.Solution.FullName);

            // Create a new document repository for the solution
            var solutionDirectory = Path.GetDirectoryName(dte.Solution.FullName);
            var repositoryDirectory = Path.Combine(solutionDirectory, ".localhistory");
            documentRepository = new DocumentRepository(solutionDirectory, repositoryDirectory);

            // Create and register a document listener that will handle save events
            documentListener = new LocalHistoryDocumentListener(documentTable, documentRepository);

            documentTable.AdviseRunningDocTableEvents(documentListener, out rdtCookie);
        }

        public void UnregisterDocumentListener()
        {
            var documentTable = (IVsRunningDocumentTable)GetGlobalService(typeof(SVsRunningDocumentTable));

            documentTable.UnadviseRunningDocTableEvents(rdtCookie);
        }

        public void RegisterSelectionListener()
        {
            var selectionMonitor = (IVsMonitorSelection)GetGlobalService(typeof(SVsShellMonitorSelection));

            selectionMonitor.AdviseSelectionEvents(this, out selectionCookie);
        }

        public void UnregisterSelectionListener()
        {
            var selectionMonitor = (IVsMonitorSelection)GetGlobalService(typeof(SVsShellMonitorSelection));

            selectionMonitor.UnadviseSelectionEvents(selectionCookie);
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew,
            IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            // The selected item can be a Solution, Project, meta ProjectItem or file ProjectItem

            // Don't update the tool window if the selection has not changed
            if (itemidOld == itemidNew)
            {
                return VSConstants.E_NOTIMPL;
            }

            // Don't update the tool window if it doesn't exist
            if (toolWindow == null)
            {
                return VSConstants.E_NOTIMPL;
            }

            // Don't update the tool window if it isn't visible
            var windowFrame = (IVsWindowFrame)toolWindow.Frame;
            if (windowFrame.IsVisible() == VSConstants.S_FALSE)
            {
                return VSConstants.E_NOTIMPL;
            }

            Debug.WriteLine(itemidOld + "->" + itemidNew);

            var si = dte.SelectedItems.Item(1);
            var item = si.ProjectItem;

            // Solutions and Projects don't have ProjectItems
            if (item != null && item.FileCount != 0)
            {
                var filePath = item.FileNames[0];

                // Only update for project items that exist (Not all of them do).
                if (File.Exists(filePath))
                {
                    UpdateToolWindow(filePath);

                    return VSConstants.E_NOTIMPL;
                }
            }

            ClearToolWindow();

            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void ProjectItemContextMenuHandler(object sender, EventArgs e)
        {
            var filePath = dte.SelectedItems.Item(1).ProjectItem.FileNames[0];

            if (File.Exists(filePath))
            {
                ShowToolWindow();

                UpdateToolWindow(filePath);
            }
        }

        private void ClearToolWindow()
        {
            if (toolWindow != null)
            {
                // Provide the control with the Visual Studio Difference Service to compare files
                var control = (LocalHistoryControl)toolWindow.Content;

                toolWindow.Caption = "Local History";
                control.LatestDocument = null;
                control.DocumentItems.Clear();
            }
        }

        private void ShowToolWindow()
        {
            if (toolWindow == null)
            {
                // Get the instance number 0 of this tool window. This window is single instance so this instance
                // is actually the only one.
                // The last flag is set to true so that if the tool window does not exists it will be created.
                toolWindow = FindToolWindow(typeof(LocalHistoryToolWindow), 0, true);
                if (toolWindow == null || toolWindow.Frame == null)
                {
                    throw new NotSupportedException(Resources.CanNotCreateWindow);
                }

                // Provide the control with the Visual Studio Difference Service to compare files
                var control = (LocalHistoryControl)toolWindow.Content;

                // TODO: remove this
                // BUG: This will cause a null pointer exception if no solution is open when the user opens the tool window.
                documentRepository.Control = control;
            }

            // Make sure the tool window is visible to the user
            var windowFrame = (IVsWindowFrame)toolWindow.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        public void UpdateToolWindow(string filePath)
        {
            var revisions = documentRepository.GetRevisions(filePath);

            // Update the tool window
            var control = (LocalHistoryControl)toolWindow.Content;
            toolWindow.Caption = "Local History - " + Path.GetFileName(filePath);

            // Remove all revisions from the revision list that belong to the previous document 
            control.DocumentItems.Clear();

            // Add the project item and its history to the revision list
            control.LatestDocument = new DocumentNode(filePath, filePath, Path.GetFileName(filePath), DateTime.Now);
            foreach (var revision in revisions)
            {
                control.DocumentItems.Add(revision);
            }
        }

        #region Unused IVsSolutionEvents

        public int OnAfterLoadProject(
            IVsHierarchy pStubHierarchy,
            IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterOpenProject(
            IVsHierarchy pHierarchy,
            int fAdded)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(
            IVsHierarchy pHierarchy,
            int fRemoved)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(
            object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(
            IVsHierarchy pRealHierarchy,
            IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(
            IVsHierarchy pHierarchy,
            int fRemoving,
            ref int pfCancel)
        {
            pfCancel = VSConstants.S_OK;

            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(
            object pUnkReserved,
            ref int pfCancel)
        {
            pfCancel = VSConstants.S_OK;

            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject(
            IVsHierarchy pRealHierarchy,
            ref int pfCancel)
        {
            pfCancel = VSConstants.S_OK;

            return VSConstants.S_OK;
        }

        #endregion

        #region Unused IVsSelectionEvents

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            return VSConstants.E_NOTIMPL;
        }

        #endregion
    }
}
