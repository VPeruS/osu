﻿// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using osu.Desktop.Overlays;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Screens.Menu;
using OpenTK.Input;

namespace osu.Desktop
{
    internal class OsuGameDesktop : OsuGame
    {
        private VersionManager versionManager;

        public OsuGameDesktop(string[] args = null)
            : base(args)
        {
        }

        public override Storage GetStorageForStableInstall()
        {
            try
            {
                return new StableStorage();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// A method of accessing an osu-stable install in a controlled fashion.
        /// </summary>
        private class StableStorage : DesktopStorage
        {
            protected override string LocateBasePath()
            {
                Func<string, bool> checkExists = p => Directory.Exists(Path.Combine(p, "Songs"));

                string stableInstallPath;

                try
                {
                    using (RegistryKey key = Registry.ClassesRoot.OpenSubKey("osu"))
                        stableInstallPath = key?.OpenSubKey(@"shell\open\command")?.GetValue(String.Empty).ToString().Split('"')[1].Replace("osu!.exe", "");

                    if (checkExists(stableInstallPath))
                        return stableInstallPath;
                }
                catch
                {
                }

                stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"osu!");
                if (checkExists(stableInstallPath))
                    return stableInstallPath;

                stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osu");
                if (checkExists(stableInstallPath))
                    return stableInstallPath;

                return null;
            }

            public StableStorage()
                : base(string.Empty)
            {
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            LoadComponentAsync(versionManager = new VersionManager { Depth = int.MinValue });

            ScreenChanged += s =>
            {
                if (s is Intro && s.ChildScreen == null)
                {
                    Add(versionManager);
                    versionManager.State = Visibility.Visible;
                }
            };
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            var desktopWindow = host.Window as DesktopGameWindow;
            if (desktopWindow != null)
            {
                desktopWindow.CursorState |= CursorState.Hidden;

                desktopWindow.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "lazer.ico"));
                desktopWindow.Title = Name;

                desktopWindow.FileDrop += (sender, e) => { dragDrop(e); };
            }
        }

        private void dragDrop(FileDropEventArgs e)
        {
            if (Path.GetExtension(e.FileName) == @".osz")
                Task.Run(() => BeatmapManager.Import(e.FileName));
            else if (Path.GetExtension(e.FileName) == @".osr")
                Task.Run(() =>
                {
                    var score = ScoreStore.ReadReplayFile(e.FileName);
                    Schedule(() => LoadScore(score));
                });
        }

        private static readonly string[] allowed_extensions = { @".osz", @".osr" };
    }
}
