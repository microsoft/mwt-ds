//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Research.MultiWorldTesting.Contract
{
    public class ApplicationTransferMetadata
    {
        public string ApplicationID { get; set; }

        public string ConnectionString { get; set; }

        public string ModelId { get; set; }

        public int ExperimentalUnitDuration { get; set; }

        public bool IsExplorationEnabled { get; set; }

        public string ModelBlobUri { get; set; }

        public string SettingsBlobUri { get; set; }
    }
}
