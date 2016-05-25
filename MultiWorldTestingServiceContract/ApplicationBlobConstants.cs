//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Research.MultiWorldTesting.Contract
{
    public static class ApplicationBlobConstants
    {
        // Model blobs
        public static readonly string ModelContainerName = "mwt-models";
        public static readonly string LatestModelBlobName = "current";

        // Settings blobs
        public static readonly string SettingsContainerName = "mwt-settings";
        public static readonly string LatestClientSettingsBlobName = "client";
        public static readonly string LatestTrainerSettingsBlobName = "trainer";
    }
}
