//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Research.MultiWorldTesting.Contract
{
    public class ApplicationBlobConstants
    {
        // Model blobs
        public const string ModelContainerName = "mwt-models";
        public const string LatestModelBlobName = "current";

        // Settings blobs
        public const string SettingsContainerName = "mwt-settings";
        public const string LatestClientSettingsBlobName = "client";
        public const string LatestTrainerSettingsBlobName = "trainer";
        public const string LatestExtraSettingsBlobName = "extra";

        public const string OfflineEvalContainerName = "mwt-offline-eval";
    }
}
