﻿/*
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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Intel.LocalHistory.Utilities
{
    internal class IVsSelectionEventsAdapter : IVsSelectionEvents
    {
        public virtual int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew,
            IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.E_NOTIMPL;
        }
    }
}
