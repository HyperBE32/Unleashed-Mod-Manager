﻿using System;
using System.IO;
using System.Linq;
using Unleash.Messenger;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using Unify.Networking.GameBanana;

// Sonic '06 Mod Manager is licensed under the MIT License:
/*
 * MIT License

 * Copyright (c) 2020 Knuxfan24
 * Copyright (c) 2020 Gabriel (HyperPolygon64)

 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

namespace Unleash.Environment3
{
    static class Program
    {
        public static readonly string VersionNumberDev = "Version 1.0",
                                      VersionNumber    = $"Version 1.0-indev-{DateTime.Now.ToString("ddMMyy")}r3";

        public static string ApplicationData    = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                             _7Zip              = $"{ApplicationData}\\Unleash\\Tools\\7z.exe",
                             Arctool            = $"{ApplicationData}\\Unleash\\Tools\\arctool.exe",
                             XexTool            = $"{ApplicationData}\\Unleash\\Tools\\xextool.exe",
                             pkgtool            = $"{ApplicationData}\\Unleash\\Tools\\pkgtool.exe",
                             vcruntime          = $"{ApplicationData}\\Unleash\\Tools\\vcruntime140_1.dll",
                             unlub              = $"{ApplicationData}\\Unleash\\Tools\\unlub.jar",
                             Patches            = $"{ApplicationData}\\Unleash\\Patches\\",
                             ProtocolManager    = $"{ApplicationData}\\Unleash\\Tools\\Protocol Manager.exe",
                             scetool            = $"{ApplicationData}\\Unleash\\Tools\\scetool.exe",
                             zlib               = $"{ApplicationData}\\Unleash\\Tools\\zlib1.dll",
                             make_fself         = $"{ApplicationData}\\Unleash\\Tools\\make_fself.exe",
                             scetool_keys       = $"{ApplicationData}\\Unleash\\Tools\\data\\keys",
                             scetool_ldr_curves = $"{ApplicationData}\\Unleash\\Tools\\data\\ldr_curves",
                             scetool_vsh_curves = $"{ApplicationData}\\Unleash\\Tools\\data\\vsh_curves";

        [STAThread]

        static void Main(string[] args) {

            #region Write required pre-requisites to the Tools directory
            if (!Directory.Exists($"{ApplicationData}\\Unleash\\Tools\\"))
                Directory.CreateDirectory($"{ApplicationData}\\Unleash\\Tools\\");

            if (!Directory.Exists($"{ApplicationData}\\Unleash\\Tools\\data\\"))
                Directory.CreateDirectory($"{ApplicationData}\\Unleash\\Tools\\data\\");

            if (!Directory.Exists(Patches))
                Directory.CreateDirectory(Patches);

            if (!File.Exists(_7Zip))
                File.WriteAllBytes(_7Zip, Properties.Resources._7z);

            if (!File.Exists(Arctool))
                File.WriteAllBytes(Arctool, Properties.Resources.arctool);

            if (!File.Exists(XexTool))
                File.WriteAllBytes(XexTool, Properties.Resources.xextool);

            if (!File.Exists(pkgtool))
                File.WriteAllBytes(pkgtool, Properties.Resources.pkgtool);

            if (!File.Exists(vcruntime))
                File.WriteAllBytes(vcruntime, Properties.Resources.vcruntime140_1);

            if (!File.Exists(unlub))
                File.WriteAllBytes(unlub, Properties.Resources.unlub);

            if (!File.Exists(ProtocolManager))
                File.WriteAllBytes(ProtocolManager, Properties.Resources.Protocol_Manager);

            if (!File.Exists(scetool))
                File.WriteAllBytes(scetool, Properties.Resources.scetool);

            if (!File.Exists(zlib))
                File.WriteAllBytes(zlib, Properties.Resources.zlib1);

            if (!File.Exists(make_fself))
                File.WriteAllBytes(make_fself, Properties.Resources.make_fself);

            if (!File.Exists(scetool_keys))
                File.WriteAllBytes(scetool_keys, Properties.Resources.keys);

            if (!File.Exists(scetool_ldr_curves))
                File.WriteAllBytes(scetool_ldr_curves, Properties.Resources.ldr_curves);

            if (!File.Exists(scetool_vsh_curves))
                File.WriteAllBytes(scetool_vsh_curves, Properties.Resources.vsh_curves);
            #endregion

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

#if !DEBUG
            try {
#endif
                // Ensure application can't be run more than once
                if ((Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)).Count() > 1) == false) {
                    if (args.Length > 0) {
                        if (args[0] == "-banana") {
                            string[] getIDs = args[1].Remove(0, 40).Split(','); // Split URL
                            string modType = string.Empty;
                            int downloadID = 0;
                            int modID = 0;
                            int i = 0;

                            //Get IDs from URL
                            foreach (var item in getIDs)
                                if      (i == 0) { int.TryParse(item, out downloadID); { i++; } }
                                else if (i == 1) { modType = item; i++; }
                                else if (i == 2) { int.TryParse(item, out modID); { i++; } }

                            var mod = new GBAPIItemDataBasic(modType, modID);
                            if (GBAPI.RequestItemData(mod)) {
                                new ModOneClickInstall(mod, args[1], downloadID, modID).ShowDialog(); // Load 1-Click Installer
                                Application.Run(new UnifyEnvironment()); // Load everything after
                            }
                        }
                    } else
                        // Load everything
                        Application.Run(new UnifyEnvironment());

                // If application is running, just load the 1-Click Installer only
                } else if (args.Length > 0) {
                    if (args[0] == "-banana") {
                        string[] getIDs = args[1].Remove(0, 40).Split(','); // Split URL
                        string modType = string.Empty;
                        int downloadID = 0;
                        int modID = 0;
                        int i = 0;

                        //Get IDs from URL
                        foreach (var item in getIDs)
                            if      (i == 0) { int.TryParse(item, out downloadID); { i++; } }
                            else if (i == 1) { modType = item; i++; }
                            else if (i == 2) { int.TryParse(item, out modID); { i++; } }

                        var mod = new GBAPIItemDataBasic(modType, modID);
                        if (GBAPI.RequestItemData(mod)) new ModOneClickInstall(mod, args[1], downloadID, modID).ShowDialog();
                    }
                }
#if !DEBUG
            } catch (Exception ex) {
                DialogResult confirmation = UnifyMessenger.UnifyMessage.ShowDialog($"Failed to load your settings... Please report this error immediately!\n\n{ex.ToString()}\n\n" +
                                                                                   "Click OK to reset Sonic '06 Mod Manager.",
                                                                                   "Settings failed", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                if (confirmation == DialogResult.OK) Program.Reset(); // Reset settings
                else Process.GetCurrentProcess().Kill(); // Quit immediately
            }
#endif
        }

        /// <summary>
        /// Erases all user settings for Sonic '06 Mod Manager.
        /// </summary>
        public static void Reset() {
            try {
                string modManagerDataPath = Path.Combine(ApplicationData, "Unleash");

                // Erases the Unify directory, containing Tools and user settings
                DirectoryInfo modManagerData = new DirectoryInfo(modManagerDataPath);
                if (Directory.Exists(modManagerDataPath)) {
                    foreach (FileInfo file in modManagerData.GetFiles()) file.Delete();
                    foreach (DirectoryInfo directory in modManagerData.GetDirectories()) directory.Delete(true);
                }
                Application.Restart();
            } catch { }
        }
    }
}