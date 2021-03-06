﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class TracksManager : AcManagerNew<TrackObject> {
        public static TracksManager Instance { get; private set; }

        public static TracksManager Initialize() {
            if (Instance != null) throw new Exception("Already initialized");
            return Instance = new TracksManager();
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.TracksDirectories;

        public override TrackObject GetDefault() {
            return base.GetById(@"imola") ?? base.GetDefault();
        }

        public override TrackObject GetById(string id) {
            return base.GetById(id.Contains('/') ? id.Split('/')[0] : id);
        }

        public override Task<TrackObject> GetByIdAsync(string id) {
            return base.GetByIdAsync(id.Contains('/') ? id.Split('/')[0] : id);
        }

        /// <summary>
        /// Gets track layout by ID like “ks_nordschleife-nordschleife” splitting it by “-” (but firstly 
        /// tries to find a track by the whole thing for tracks like “trento-bondone”). Weird: there are
        /// so many perfectly good candidates to be a delimiter in this case (“/”, “:”, “@” to name a few),
        /// and yet Kunos managed to select not only the character which could be in directory’s name, but
        /// also the character actually used by themself in one of their track.
        /// </summary>
        /// <param name="id">ID like “ks_nordschleife-nordschleife”.</param>
        /// <returns>Track layout.</returns>
        [CanBeNull]
        public TrackObjectBase GetLayoutByKunosId([NotNull] string id) {
            var layout = GetLayoutById(id);
            if (layout != null) return layout;

            var index = id.LastIndexOf('-');
            if (index == -1) return null;

            while (index != -1 && index > 0 && index < id.Length - 1) {
                var candidate = GetLayoutById(id.Substring(0, index) + '/' + id.Substring(index + 1));
                if (candidate != null) return candidate;

                index = id.LastIndexOf('-', index - 1);
            }

            return null;
        }

        /// <summary>
        /// Gets track layout by ID like “ks_nordschleife-nordschleife” splitting it by “-” (but firstly 
        /// tries to find a track by the whole thing for tracks like “trento-bondone”). Weird: there are
        /// so many perfectly good candidates to be a delimiter in this case (“/”, “:”, “@” to name a few),
        /// and yet Kunos managed to select not only the character which could be in directory’s name, but
        /// also the character actually used by themself in one of their track.
        /// </summary>
        /// <param name="id">ID like “ks_nordschleife-nordschleife”.</param>
        /// <returns>Track layout.</returns>
        [ItemCanBeNull]
        public async Task<TrackObjectBase> GetLayoutByKunosIdAsync([NotNull] string id) {
            return await GetLayoutByIdAsync(id) ?? (id.Contains(@"-") ? await GetLayoutByIdAsync(id.ReplaceLastOccurrence(@"-", @"/")) : null);
        }

        [CanBeNull]
        public TrackObjectBase GetLayoutById([NotNull] string id) {
            if (!id.Contains('/')) return base.GetById(id);
            return base.GetById(id.Split('/')[0])?.GetLayoutById(id);
        }

        [ItemCanBeNull]
        public async Task<TrackObjectBase> GetLayoutByIdAsync([NotNull] string id) {
            if (!id.Contains('/')) return await base.GetByIdAsync(id);
            return (await base.GetByIdAsync(id.Split('/')[0]))?.GetLayoutById(id);
        }

        [CanBeNull]
        public TrackObjectBase GetLayoutById([NotNull] string trackId, [CanBeNull] string layoutId) {
            if (trackId == null) throw new ArgumentNullException(nameof(trackId));
            return layoutId == null ? GetById(trackId) : GetById(trackId)?.GetLayoutByLayoutId(layoutId);
        }

        [ItemCanBeNull]
        public async Task<TrackObjectBase> GetLayoutByIdAsync([NotNull] string trackId, [CanBeNull] string layoutId) {
            if (trackId == null) throw new ArgumentNullException(nameof(trackId));
            return layoutId == null ? await GetByIdAsync(trackId) : (await GetByIdAsync(trackId))?.GetLayoutByLayoutId(layoutId);
        }

        protected override TrackObject CreateAcObject(string id, bool enabled) {
            return new TrackObject(this, id, enabled);
        }

        private static readonly string[] WatchedFileNames = {
            @"preview.png",
            @"outline.png",
            @"ui_track.json"
        };

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;
            if (WatchedFileNames.Contains(Path.GetFileName(filename).ToLowerInvariant())) {
                return false;
            }

            var relative = FileUtils.GetRelativePath(filename, objectLocation);

            var splitted = FileUtils.Split(relative);
            if (!string.Equals(splitted[0], @"ui", StringComparison.OrdinalIgnoreCase) || splitted.Length > 3) {
                return true;
            }

            return false;
        }
    }
}
