//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Research.MultiWorldTesting.Contract
{
    public static class ApplicationBlobConstants
    {
        public static readonly string ApplicationBlobLocationContainerName = "app-locations";
        
        // Model blobs
        public static readonly string ModelContainerName = "mwt-models-{0}";
        public static readonly string LatestModelBlobName = "current";

        // Settings blobs
        public static readonly string SettingsContainerName = "mwt-settings-{0}";
        public static readonly string LatestSettingsBlobName = "settings";
    }
}
