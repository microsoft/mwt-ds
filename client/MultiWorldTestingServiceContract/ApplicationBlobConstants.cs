//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Research.MultiWorldTesting.Contract
{
    /// <summary>
    /// Various blob and container blob names
    /// </summary>
    public class ApplicationBlobConstants
    {
        /// <summary>
        /// Container name for models.
        /// </summary>
        public const string ModelContainerName = "mwt-models";

        /// <summary>
        /// Blob name for latest model.
        /// </summary>
        public const string LatestModelBlobName = "current";

        /// <summary>
        /// Container name for settings.
        /// </summary>
        public const string SettingsContainerName = "mwt-settings";

        /// <summary>
        /// Blob name for client settings.
        /// </summary>
        public const string LatestClientSettingsBlobName = "client";

        /// <summary>
        /// Blob name for trainer settings.
        /// </summary>
        public const string LatestTrainerSettingsBlobName = "trainer";

        /// <summary>
        /// Blob name for extra settings.
        /// </summary>
        public const string LatestExtraSettingsBlobName = "extra";

        /// <summary>
        /// Container name for offline evaluation.
        /// </summary>
        public const string OfflineEvalContainerName = "mwt-offline-eval";
    }
}
