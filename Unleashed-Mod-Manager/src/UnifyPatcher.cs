﻿using System;
using System.IO;
using System.Text;
using System.Linq;
using ArcPackerLib;
using Unleash.Messenger;
using Microsoft.Win32;
using Unleash.Serialisers;
using Unleash.Environment3;
using System.Diagnostics;
using Unleash.Globalisation;
using System.Windows.Forms;
using System.IO.Compression;
using System.Collections.Generic;

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

namespace Unleash.Patcher
{
    class ModEngine
    {
        public static List<string> skipped = new List<string>();  // Define the skipped list for error tracking

        /// <summary>
        /// Installs the specified mods (requires for statement iteration for more than one mod).
        /// </summary>
        /// <param name="mod">File path to the mod's INI file.</param>
        /// <param name="name">Name of the mod by Title key.</param>
        public static void InstallMods(string mod, string name) {
            string platform = INI.DeserialiseKey("Platform", mod); // Deserialise 'Platform' key

            //Skip the mod if the platform is invalid
            string system = Literal.System(Properties.Settings.Default.Path_GameDirectory);
            if (system != platform && platform != "All Systems") {
                skipped.Add($"► {name} (failed because the mod was not targeted for the {system})");
                return;
            }

            // Search for all files with specified LINQ filters
            List<string> files = Directory.GetFiles(Path.GetDirectoryName(mod), "*.*", SearchOption.AllDirectories)
                                .Where(s => s.Contains(".ar.") ||
                                            s.EndsWith(".arl") ||
                                            s.EndsWith(".dds") ||
                                            s.EndsWith("default.xex") ||
                                            s.EndsWith("EBOOT.BIN") ||
                                            s.EndsWith(".sfd") ||
                                            s.EndsWith(".pfd") ||
                                            s.EndsWith(".csb") ||
                                            s.EndsWith(".cpk")).ToList();

            foreach (string file in files) {
                // Absolute file path
                string filePath = file.Remove(0, Path.GetDirectoryName(mod).Length);

                // Absolute file path (from the mod) combined with the game directory
                string vanillaFilePath = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), filePath.Substring(1));

                // Backup file path derived from the file about to be overwritten
                string targetFilePath = $"{vanillaFilePath}_back";

                // Backup the original file
                if (File.Exists(vanillaFilePath) && !File.Exists(targetFilePath)) File.Copy(vanillaFilePath, targetFilePath);

                // Copy the modified data
                if (RushInterface._debug) Console.WriteLine($"Copying: {file}");
                File.Copy(file, vanillaFilePath, true);
            }
        }

        /// <summary>
        /// Uninstalls all mods.
        /// </summary>
        public static void UninstallMods() {
            // If the game directory is empty/doesn't exist, ignore request
            if (Properties.Settings.Default.Path_GameDirectory != string.Empty || File.Exists(Properties.Settings.Default.Path_GameDirectory)) {
                // Search for all files
                List<string> files = Directory.GetFiles(
                                               Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory),
                                               "*.*_back",
                                               SearchOption.AllDirectories)
                                               .Where(s => !s.Contains(".ar.")).ToList();

                foreach (string file in files) {
                    string originalName = file.Remove(file.Length - 5);

                    if (Path.GetExtension(file) == ".arl_back") {
                        uint arCount;

                        using (var stream = File.Open(file, FileMode.Open, FileAccess.Read)) {
                            stream.Position = 0x04;
                            arCount = (uint)stream.ReadByte();
                        }

                        for (int i = 0; i < arCount; i++) {
                            if (RushInterface._debug) Console.WriteLine($"Removing: {file}");

                            if (i < 10) {
                                string archiveSub10 = $"{file.Remove(file.Length - 6)}.0{i}";
                                if (File.Exists(archiveSub10)) {
                                    File.Delete(archiveSub10); // Delete file with last five characters set to '_back'
                                    if (File.Exists($"{archiveSub10}_back")) File.Move($"{archiveSub10}_back", archiveSub10); // Remove last five characters ('_back')
                                }
                            } else {
                                string archive = $"{file.Remove(file.Length - 6)}.{i}";
                                if (File.Exists(archive)) {
                                    File.Delete(archive); // Delete file with last five characters set to '_back'
                                    if (File.Exists($"{archive}_back")) File.Move($"{archive}_back", archive); // Remove last five characters ('_back')
                                }
                            }
                        }
                    }

                    if (File.Exists(originalName)) {
                        if (RushInterface._debug) Console.WriteLine($"Removing: {file}");
                        File.Delete(originalName); // Delete file with last five characters set to '_back'
                        if (File.Exists(file)) File.Move(file, originalName); // Remove last five characters ('_back')
                    }
                }
            }
        }

        /// <summary>
        /// Removes redirected save data.
        /// </summary>
        public static void UninstallSaves(ListView.ListViewItemCollection listViewItems) {
            if (Properties.Settings.Default.Path_SaveData != string.Empty || File.Exists(Properties.Settings.Default.Path_SaveData)) {
                foreach (ListViewItem mod in listViewItems) {
                    // Basically just to check 'SonicNextSaveData.bin' as a directory
                    string saveLocation = Path.GetDirectoryName(Path.GetDirectoryName(Properties.Settings.Default.Path_SaveData));

                    // Deserialise 'Save' key
                    string savedata = INI.DeserialiseKey("Save", mod.SubItems[6].Text);

                    if (savedata != string.Empty) { // Speeds things up a bit - ensures it's not checking a default null parameter
                        if (Literal.Emulator(Properties.Settings.Default.Path_GameDirectory) == "Xenia") {
                            string[] saves = Array.Empty<string>();

                            // Get all backup directories
                            if (Directory.Exists(saveLocation)) saves = Directory.GetDirectories(saveLocation, "SonicNextSaveData.bin_back", SearchOption.AllDirectories);

                            foreach (var dir in saves) {
                                // Original save data path
                                string saveFile = Path.Combine(dir.ToString().Remove(dir.Length - 5), Path.GetFileName(dir.ToString().Remove(dir.Length - 5)));

                                // Copy redirected save data back to the mod's directory (keeps user progress)
                                if (File.Exists(saveFile)) {
                                    Console.WriteLine($"Removing: {dir}");
                                    if (savedata != string.Empty) File.Copy(saveFile, Path.Combine(Path.GetDirectoryName(mod.SubItems[6].Text), "savedata.360"), true);
                                }

                                // Recursively erase redirected save data
                                if (Directory.Exists(dir.ToString().Remove(dir.Length - 5))) {
                                    Console.WriteLine($"Removing: {dir}");
                                    Directory.Delete(dir.ToString().Remove(dir.Length - 5), true);
                                }

                                // Restore original save data
                                Directory.Move(dir.ToString(), dir.ToString().Remove(dir.Length - 5));
                            }
                        } else if (Literal.Emulator(Properties.Settings.Default.Path_GameDirectory) == "RPCS3") {
                            string[] saves = Array.Empty<string>();

                            // Original save data path
                            if (Directory.Exists(saveLocation)) saves = Directory.GetFiles(saveLocation, "SYS-DATA_back", SearchOption.AllDirectories);

                            foreach (var file in saves) {
                                string saveFile = Path.Combine(file.ToString().Remove(file.Length - 5), Path.GetFileName(file.ToString().Remove(file.Length - 5)));

                                // Copy redirected save data back to the mod's directory (keeps user progress)
                                if (File.Exists(saveFile)) {
                                    Console.WriteLine($"Removing: {file}");
                                    if (savedata != string.Empty) File.Copy(saveFile, Path.Combine(Path.GetDirectoryName(mod.SubItems[6].Text), "savedata.ps3"), true);
                                }

                                // Erase redirected save data
                                if (File.Exists(file.ToString().Remove(file.Length - 5))) {
                                    Console.WriteLine($"Removing: {file}");
                                    File.Delete(file.ToString().Remove(file.Length - 5));
                                }

                                // Restore original save data
                                File.Move(file.ToString(), file.ToString().Remove(file.Length - 5));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts an archive to a temporary location.
        /// </summary>
        public static string UnpackARC(string arc, string tempPath) {
            Directory.CreateDirectory(tempPath); // Create temporary location
            File.Copy(arc, Path.Combine(tempPath, Path.GetFileName(arc))); // Copy archive to temporary location

            // Extracts the archive in the temporary location
            var unpack = new ProcessStartInfo() {
                FileName = Program.Arctool,
                Arguments = $"-d \"{Path.Combine(tempPath, Path.GetFileName(arc))}\"",
                WorkingDirectory = Path.GetDirectoryName(Program.Arctool),
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var Unpack = Process.Start(unpack);
            Unpack.WaitForExit();
            Unpack.Close();

            return tempPath;
        }

        /// <summary>
        /// Repacks an archive from a temporary location.
        /// </summary>
        public static void RepackARC(string arc, string output) {
            ArcPacker repack = new ArcPacker();
            repack.WriteArc(output, Path.Combine(arc, Path.GetFileNameWithoutExtension(output)));

            // Erases temporary repack data
            try {
                DirectoryInfo tempData = new DirectoryInfo(arc);
                if (Directory.Exists(arc)) {
                    foreach (FileInfo file in tempData.GetFiles()) file.Delete();
                    foreach (DirectoryInfo directory in tempData.GetDirectories()) directory.Delete(true);
                    Directory.Delete(arc);
                }
            } catch { }
        }
    }

    class PatchEngine
    {
        static string _archive = string.Empty;
        static string _archiveLocation = string.Empty;
        static List<string> _ignoreList = new List<string>();

        /// <summary>
        /// Installs the specified patches (requires for statement iteration for more than one mod).
        /// </summary>
        /// <param name="patch">File path to the patch's MLUA file.</param>
        /// <param name="name">Name of the patch by Title key.</param>
        public static void InstallPatches(string patch, string name) {
            string platform = Lua.DeserialiseParameter("Platform", patch, true); // Deserialise 'Platform' parameter

            bool allSystemsMode = platform == "All Systems",
                 systemReached  = false;

            // Skip the patch if the platform is invalid
            string system = Literal.System(Properties.Settings.Default.Path_GameDirectory);
            if (system != platform && !allSystemsMode) {
                ModEngine.skipped.Add($"► {name} (failed because the patch was not targeted for the {system})");
                return;
            }

            // Begin reading patch script
            using (StreamReader patchScript = new StreamReader(patch, Encoding.Default)) {
                string line;
                while ((line = patchScript.ReadLine()) != null) {

                    // If the platform is 'All Systems' then check if it shall proceed if labels are involved
                    if (allSystemsMode) {
                        if (line.StartsWith("All Systems") || line.StartsWith(Literal.System(Properties.Settings.Default.Path_GameDirectory))) systemReached = true;
                        else if (line.StartsWith(Literal.OppositeSystem(Properties.Settings.Default.Path_GameDirectory))) systemReached = false;
                    } else
                        // System is specific, so proceed as normal
                        systemReached = true;

                    if (systemReached) {
                        if (line.StartsWith("BeginBlock")) {
                            string _BeginBlock = Lua.DeserialiseParameter("BeginBlock", line, false); // Deserialise 'BeginBlock' parameter

                            if (_BeginBlock != string.Empty)
                                BeginBlock(Literal.CoreReplace(_BeginBlock));
                        }

                        if (line.StartsWith("Dec")) {
                            if (line.StartsWith("DecryptExecutable"))
                                DecryptExecutable();
                            else if (line.StartsWith("DecompressExecutable"))
                                DecompressExecutable();
                        }

                        if (line.StartsWith("Enc")) {
                            if (line.StartsWith("EncryptExecutable"))
                                EncryptExecutable();
                        }

                        if (line.StartsWith("Write")) {
                            string[] _WriteByte   = Lua.DeserialiseParameterList("WriteByte", line, false),   // Deserialise 'WriteByte' parameter
                                     _WriteNopPPC = Lua.DeserialiseParameterList("WriteNopPPC", line, false), // Deserialise 'WriteNopPPC' parameter
                                     _WriteBase64 = Lua.DeserialiseParameterList("WriteBase64", line, false); // Deserialise 'WriteBase64' parameter

                            if (line.StartsWith("WriteByte") && _WriteByte.Length != 0)
                                WriteByte(Literal.CoreReplace(_WriteByte[0]), Convert.ToInt32(_WriteByte[1], 16), Convert.ToByte(_WriteByte[2], 16));
                            else if (line.StartsWith("WriteNopPPC") && _WriteNopPPC.Length != 0)
                                WriteNopPPC(Literal.CoreReplace(_WriteNopPPC[0]), Convert.ToInt32(_WriteNopPPC[1], 16));
                            else if (line.StartsWith("WriteBase64") && _WriteBase64.Length != 0)
                                WriteBase64(Literal.CoreReplace(_WriteBase64[0]), _WriteBase64[1]);
                        }

                        if (line.StartsWith("Rename")) {
                            string[] _Rename            = Lua.DeserialiseParameterList("Rename", line, false),            // Deserialise 'Rename' parameter
                                     _RenameByExtension = Lua.DeserialiseParameterList("RenameByExtension", line, false); // Deserialise 'RenameByExtension' parameter

                            if (line.StartsWith("RenameByExtension") && _RenameByExtension.Length != 0)
                                RenameByExtension(Literal.CoreReplace(_RenameByExtension[0]), _RenameByExtension[1], _RenameByExtension[2]);
                            else if (_Rename.Length != 0)
                                Rename(Literal.CoreReplace(_Rename[0]), _Rename[1]);
                        }

                        if (line.StartsWith("Copy")) {
                            string[] _Copy = Lua.DeserialiseParameterList("Copy", line, false); // Deserialise 'Copy' parameter

                            if (_Copy.Length != 0)
                                Copy(Literal.CoreReplace(_Copy[0]), Literal.CoreReplace(_Copy[1]), bool.Parse(_Copy[2]));
                        }

                        if (line.StartsWith("Delete")) {
                            string _Delete = Lua.DeserialiseParameter("Delete", line, false); // Deserialise 'Delete' parameter

                            if (_Delete != string.Empty)
                                Delete(Literal.CoreReplace(_Delete));
                        }

                        if (line.StartsWith("Ignore")) {
                            string[] _Ignore = Lua.DeserialiseParameterList("Ignore", line, false); // Deserialise 'Ignore' parameter

                            if (_Ignore.Length != 0)
                                _ignoreList = _Ignore.ToList();
                        }

                        if (line.StartsWith("Parameter")) {
                            string[] _ParameterAdd = Lua.DeserialiseParameterList("ParameterAdd", line, false),     // Deserialise 'ParameterEdit' parameter
                                     _ParameterEdit = Lua.DeserialiseParameterList("ParameterEdit", line, false),     // Deserialise 'ParameterEdit' parameter
                                     _ParameterErase = Lua.DeserialiseParameterList("ParameterErase", line, false),   // Deserialise 'ParameterErase' parameter
                                     _ParameterRename = Lua.DeserialiseParameterList("ParameterRename", line, false); // Deserialise 'ParameterRename' parameter

                            if (line.StartsWith("ParameterAdd") && _ParameterAdd.Length != 0)
                                ParameterAdd(Literal.CoreReplace(_ParameterAdd[0]), _ParameterAdd[1], _ParameterAdd[2]);
                            else if (line.StartsWith("ParameterEdit") && _ParameterEdit.Length != 0)
                                ParameterEdit(Literal.CoreReplace(_ParameterEdit[0]), _ParameterEdit[1], _ParameterEdit[2]);
                            else if (line.StartsWith("ParameterErase") && _ParameterErase.Length != 0)
                                ParameterErase(Literal.CoreReplace(_ParameterErase[0]), _ParameterErase[1]);
                            else if (line.StartsWith("ParameterRename") && _ParameterRename.Length != 0)
                                ParameterRename(Literal.CoreReplace(_ParameterRename[0]), _ParameterRename[1], _ParameterRename[2]);
                        }

                        if (line.StartsWith("StringReplace")) {
                            string[] _StringReplace = Lua.DeserialiseParameterList("StringReplace", line, false); // Deserialise 'StringReplace' parameter

                            if (_StringReplace.Length != 0)
                                StringReplace(Literal.CoreReplace(_StringReplace[0]), _StringReplace[1], _StringReplace[2]);
                        }

                        if (line.StartsWith("Package")) {
                            string[] _PackageAdd = Lua.DeserialiseParameterList("PackageAdd", line, false), // Deserialise 'PackageAdd' parameter
                                     _PackageEdit = Lua.DeserialiseParameterList("PackageEdit", line, false); // Deserialise 'PackageEdit' parameter

                            if (line.StartsWith("PackageAdd") && _PackageAdd.Length != 0)
                                PackageAdd(Literal.CoreReplace(_PackageAdd[0]), _PackageAdd[1], _PackageAdd[2], _PackageAdd[3]);
                            else if (line.StartsWith("PackageEdit") && _PackageEdit.Length != 0)
                                PackageEdit(Literal.CoreReplace(_PackageEdit[0]), _PackageEdit[1], _PackageEdit[2], _PackageEdit[3]);
                        }

                        if (line.StartsWith("EndBlock")) {
                            string _EndBlock = Lua.DeserialiseParameter("EndBlock", line, false); // Deserialise 'EndBlock' parameter

                            if (_EndBlock != string.Empty)
                                EndBlock();
                        }
                    }
                }
            }
        }

        private static string BeginBlock(string location) {
            location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

            _archiveLocation = location;
            return _archive = ModEngine.UnpackARC(location, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        }

        private static void Copy(string location, string newFile, bool overwrite) {
            if (_archive != string.Empty)
                location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else {
                location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

                if (!File.Exists($"{location}_back"))
                    if (File.Exists(location)) File.Copy(location, $"{location}_back");
            }

            if (File.Exists(newFile)) File.Copy(location, newFile, overwrite);
        }

        private static void Delete(string location) {
            if (_archive != string.Empty)
                location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else {
                location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

                if (!File.Exists($"{location}_back"))
                    if (File.Exists(location)) File.Copy(location, $"{location}_back");
            }

            if (File.Exists(location)) File.Delete(location);
        }

        private static void EndBlock() {
            if (!File.Exists($"{_archiveLocation}_back"))
                File.Copy(_archiveLocation, $"{_archiveLocation}_back");

            ModEngine.RepackARC(_archive, _archiveLocation);
            _archive = _archiveLocation = string.Empty;
        }

        private static void EncryptExecutable() {
            string gameDirectory = Properties.Settings.Default.Path_GameDirectory;

            if (!File.Exists($"{gameDirectory}_back"))
                if (File.Exists(gameDirectory)) File.Copy(gameDirectory, $"{gameDirectory}_back");

            if (Literal.System(gameDirectory) == "Xbox 360")           XEX.Encrypt(gameDirectory);
            else if (Literal.System(gameDirectory) == "PlayStation 3") EBOOT.Encrypt(gameDirectory);
        }

        private static void DecryptExecutable() {
            string gameDirectory = Properties.Settings.Default.Path_GameDirectory;

            if (!File.Exists($"{gameDirectory}_back"))
                if (File.Exists(gameDirectory)) File.Copy(gameDirectory, $"{gameDirectory}_back");

            if (Literal.System(gameDirectory) == "Xbox 360")           XEX.Decrypt(gameDirectory);
            else if (Literal.System(gameDirectory) == "PlayStation 3") EBOOT.Decrypt(gameDirectory);
        }

        private static void DecompressExecutable() {
            string gameDirectory = Properties.Settings.Default.Path_GameDirectory;

            if (!File.Exists($"{gameDirectory}_back"))
                if (File.Exists(gameDirectory)) File.Copy(gameDirectory, $"{gameDirectory}_back");

            if (Literal.System(gameDirectory) == "Xbox 360") XEX.Decompress(gameDirectory);
        }

        private static void WriteByte(string location, long offset, byte _byte) {
            if (location == "Executable") location = Properties.Settings.Default.Path_GameDirectory;
            else {
                if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
                else {
                    location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

                    if (!File.Exists($"{location}_back"))
                        if (File.Exists(location)) File.Copy(location, $"{location}_back");
                }
            }

            if (File.Exists(location))
                using (FileStream stream = File.Open(location, FileMode.Open, FileAccess.Write)) {
                    stream.Position = offset; stream.WriteByte(_byte);
                }
        }

        private static void WriteNopPPC(string location, long offset) {
            if (location == "Executable") location = Properties.Settings.Default.Path_GameDirectory;
            else {
                if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
                else {
                    location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

                    if (!File.Exists($"{location}_back"))
                        if (File.Exists(location)) File.Copy(location, $"{location}_back");
                }
            }

            if (File.Exists(location)) {
                using (FileStream stream = File.Open(location, FileMode.Open, FileAccess.Write)) {
                    stream.Position = offset; stream.WriteByte(0x60);
                }

                for (int i = 1; i < 4; i++)
                    using (FileStream stream = File.Open(location, FileMode.Open, FileAccess.Write)) {
                        stream.Position = offset + i; stream.WriteByte(0x00);
                    }
            }
        }

        private static void WriteBase64(string location, string data) {
            if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else {
                location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

                if (!File.Exists($"{location}_back"))
                    if (File.Exists(location)) File.Copy(location, $"{location}_back");
            }

            byte[] bytes = Convert.FromBase64String(data);
            File.WriteAllBytes(location, bytes);
        }

        private static void Rename(string location, string _new) {
            if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else {
                location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

                if (!File.Exists($"{location}_back"))
                    if (File.Exists(location)) File.Copy(location, $"{location}_back");
            }

            string newName = Path.Combine(Path.GetDirectoryName(location), Path.GetFileName(_new)),
                   backup = $"{location}_back";

            if (!File.Exists(newName)) File.Move(location, newName);
            else if (newName == backup && File.Exists(backup)) File.Delete(location);
        }

        private static void RenameByExtension(string location, string extension, string _new) {
            if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

            foreach (string file in Directory.GetFiles(location, extension, SearchOption.TopDirectoryOnly)) {
                string newName = Path.ChangeExtension(file, _new),
                       backup = $"{file}_back";

                if (!File.Exists(Path.ChangeExtension(file, _new))) File.Move(file, newName);
                else if (newName == backup && File.Exists(backup)) File.Delete(file);
            }
        }

        private static void ParameterAdd(string location, string parameter, string value) {
            if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

            if (File.Exists(location)) {
                DecompileLua(location);
                value = value.Replace("\\\"", "\"");
                List<string> scriptList = File.ReadAllLines(location).ToList();
                scriptList.Add($"{parameter} = {value}");
                File.WriteAllLines(location, scriptList);
            } else if (Directory.Exists(location)) {
                foreach (string luaData in Directory.GetFiles(location, "*.lub", SearchOption.TopDirectoryOnly)) {
                    if (!_ignoreList.Any(s => Path.GetFileName(luaData).Contains(s))) {
                        DecompileLua(luaData);
                        value = value.Replace("\\\"", "\"");
                        List<string> scriptList = File.ReadAllLines(location).ToList();
                        scriptList.Add($"{parameter} = {value}");
                        File.WriteAllLines(location, scriptList);
                    }
                }
            }
        }

        private static void ParameterEdit(string location, string parameter, string value) {
            if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

            if (File.Exists(location)) {
                DecompileLua(location);
                string[] script = File.ReadAllLines(location);
                int lineCount = 0;

                value = value.Replace("\\\"", "\"");
                foreach (string line in script) {
                    if (line.StartsWith(parameter)) {
                        string[] split = line.Split(' ');
                        split[2] = value;
                        for (int i = 3; i < split.Count(); i++) split[i] = string.Empty;
                        script[lineCount] = string.Join(" ", split);
                        break;
                    }
                    lineCount++;
                }

                File.WriteAllLines(location, script);
            } else if (Directory.Exists(location)) {
                foreach (string luaData in Directory.GetFiles(location, "*.lub", SearchOption.TopDirectoryOnly)) {
                    if (!_ignoreList.Any(s => Path.GetFileName(luaData).Contains(s))) {
                        DecompileLua(luaData);
                        List<string> script = File.ReadAllLines(luaData).ToList();
                        int lineCount = 0;

                        value = value.Replace("\\\"", "\"");
                        foreach (string line in script) {
                            if (line.StartsWith(parameter)) {
                                string[] split = line.Split(' ');
                                split[2] = value;
                                for (int i = 3; i < split.Count(); i++) split[i] = string.Empty;
                                script[lineCount] = string.Join(" ", split);
                                break;
                            }
                            lineCount++;
                        }

                        File.WriteAllLines(luaData, script);
                    }
                }
            }
        }

        private static void ParameterErase(string location, string parameter) {
            if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

            if (File.Exists(location) && !_ignoreList.Contains(Path.GetFileName(location))) {
                DecompileLua(location);
                List<string> script = File.ReadAllLines(location).ToList();
                List<string> editedScript = File.ReadAllLines(location).ToList();
                int lineCount = 0;

                foreach (string line in script) {
                    if (line.Contains(parameter)) editedScript.RemoveAt(lineCount);
                    lineCount++;
                }

                File.WriteAllLines(location, editedScript);
            } else if (Directory.Exists(location)) {
                foreach (string luaData in Directory.GetFiles(location, "*.lub", SearchOption.TopDirectoryOnly)) {
                    if (!_ignoreList.Any(s => Path.GetFileName(luaData).Contains(s))) {
                        DecompileLua(luaData);
                        List<string> script = File.ReadAllLines(luaData).ToList();
                        List<string> editedScript = File.ReadAllLines(luaData).ToList();
                        int lineCount = 0;

                        foreach (string line in script) {
                            if (line.Contains(parameter)) editedScript.RemoveAt(lineCount);
                            lineCount++;
                        }

                        File.WriteAllLines(luaData, editedScript);
                    }
                }
            }
        }

        private static void ParameterRename(string location, string parameter, string _new) {
            if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

            if (File.Exists(location) && !_ignoreList.Contains(Path.GetFileName(location))) {
                DecompileLua(location);
                string[] script = File.ReadAllLines(location);
                int lineCount = 0;

                foreach (string line in script) {
                    if (line.StartsWith(parameter)) {
                        string[] split = line.Split(' ');
                        split[0] = _new;
                        script[lineCount] = string.Join(" ", split);
                        break;
                    }
                    lineCount++;
                }

                File.WriteAllLines(location, script);
            } else if (Directory.Exists(location)) {
                foreach (string luaData in Directory.GetFiles(location, "*.lub", SearchOption.TopDirectoryOnly)) {
                    if (!_ignoreList.Any(s => Path.GetFileName(luaData).Contains(s))) {
                        DecompileLua(luaData);
                        string[] script = File.ReadAllLines(location);
                        int lineCount = 0;

                        foreach (string line in script) {
                            if (line.StartsWith(parameter)) {
                                string[] split = line.Split(' ');
                                split[0] = _new;
                                script[lineCount] = string.Join(" ", split);
                                break;
                            }
                            lineCount++;
                        }

                        File.WriteAllLines(luaData, script);
                    }
                }
            }
        }

        private static void StringReplace(string location, string _string, string _new) {
            if (_archive != string.Empty) location = Path.Combine(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation)), location);
            else location = Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location);

            if (File.Exists(location) && !_ignoreList.Contains(Path.GetFileName(location))) {
                DecompileLua(location);
                string[] script = File.ReadAllLines(location);
                int lineCount = 0;

                foreach (string line in script) {
                    if (line.Contains(_string)) {
                        script[lineCount] = line.Replace(_string, _new);
                        break;
                    }
                    lineCount++;
                }

                File.WriteAllLines(location, script);
            } else if (Directory.Exists(location)) {
                foreach (string luaData in Directory.GetFiles(location, "*.lub", SearchOption.TopDirectoryOnly)) {
                    if (!_ignoreList.Any(s => Path.GetFileName(luaData).Contains(s))) {
                        DecompileLua(luaData);
                        string[] script = File.ReadAllLines(luaData);
                        int lineCount = 0;

                        foreach (string line in script) {
                            if (line == _string) {
                                line.Replace(_string, _new);
                                break;
                            }
                            lineCount++;
                        }

                        File.WriteAllLines(luaData, script);
                    }
                }
            }
        }

        private static void PackageAdd(string location, string key, string _event, string reference) {
            if (_archive != string.Empty) location = Paths.GetPathWithoutExtension(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation), location));
            else location = Paths.GetPathWithoutExtension(Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location));

            if (File.Exists($"{location}.pkg") && !_ignoreList.Contains($"{Path.GetFileName(location)}.pkg")) {
                PKG.PKGTool($"{location}.pkg");
                List<string> package = File.ReadAllLines($"{location}.txt").ToList();
                package.Add($"\"{key}\"\n{"{"}");
                package.Add($"\t\"{_event}\" = \"{reference}\";");
                package.Add("}");
                File.WriteAllLines($"{location}.txt", package);
                PKG.PKGTool($"{location}.txt");
            } else if (Directory.Exists(location)) {
                foreach (string packageData in Directory.GetFiles(location, "*.pkg", SearchOption.TopDirectoryOnly)) {
                    if (!_ignoreList.Any(s => Path.GetFileName(packageData).Contains(s))) {
                        PKG.PKGTool(packageData);
                        List<string> package = File.ReadAllLines(packageData).ToList();
                        package.Add($"\"{key}\"\n{"{"}");
                        package.Add($"\t\"{_event}\" = \"{reference}\";");
                        package.Add("}");
                        File.WriteAllLines(packageData, package);
                        PKG.PKGTool(Path.Combine(Path.GetDirectoryName(packageData), $"{Path.GetFileNameWithoutExtension(packageData)}.txt"));
                    }
                }
            }
        }

        private static void PackageEdit(string location, string key, string _event, string reference) {
            if (_archive != string.Empty) location = Paths.GetPathWithoutExtension(Path.Combine(_archive, Path.GetFileNameWithoutExtension(_archiveLocation), location));
            else location = Paths.GetPathWithoutExtension(Path.Combine(Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory), location));

            if (File.Exists($"{location}.pkg") && !_ignoreList.Contains($"{Path.GetFileName(location)}.pkg")) {
                PKG.PKGTool($"{location}.pkg");
                List<string> package = File.ReadAllLines($"{location}.txt").ToList();
                List<string> editedPackage = File.ReadAllLines($"{location}.txt").ToList();
                bool keyFound = false;
                int lineCount = 0;

                foreach (string entry in package) {
                    if (entry.StartsWith($"\"{key}\"")) keyFound = true;
                    if (entry.Contains($"\"{_event}\"")) {
                        editedPackage.RemoveAt(lineCount);
                        editedPackage.Insert(lineCount, $"\t\"{_event}\" = \"{reference}\";");
                        break;
                    }
                    if (entry.StartsWith("}") && keyFound) {
                        editedPackage.Insert(lineCount - 1, $"\t\"{_event}\" = \"{reference}\";");
                        break;
                    }
                    lineCount++;
                }

                File.WriteAllLines($"{location}.txt", editedPackage); PKG.PKGTool($"{location}.txt");
            } else if (Directory.Exists(location)) {
                foreach (string packageData in Directory.GetFiles(location, "*.pkg", SearchOption.TopDirectoryOnly)) {
                    if (!_ignoreList.Any(s => Path.GetFileName(packageData).Contains(s))) {
                        PKG.PKGTool(packageData);
                        List<string> package = File.ReadAllLines(packageData).ToList();
                        List<string> editedPackage = File.ReadAllLines($"{location}.txt").ToList();
                        bool keyFound = false;
                        int lineCount = 0;

                        foreach (string entry in package) {
                            if (entry.StartsWith($"\"{key}\"")) keyFound = true;
                            if (entry.Contains($"\"{_event}\"")) {
                                editedPackage.RemoveAt(lineCount);
                                editedPackage.Insert(lineCount, $"\t\"{_event}\" = \"{reference}\";");
                                break;
                            }
                            if (entry.StartsWith("}") && keyFound) {
                                editedPackage.Insert(lineCount - 1, $"\t\"{_event}\" = \"{reference}\";");
                                break;
                            }
                            lineCount++;
                        }

                        File.WriteAllLines(packageData, editedPackage);
                        PKG.PKGTool(Path.Combine(Path.GetDirectoryName(packageData), $"{Path.GetFileNameWithoutExtension(packageData)}.txt"));
                    }
                }
            }
        }

        public static void DecompileLua(string _file) {
            string[] readText = File.ReadAllLines(_file); //Read the Lub into an array

            if (readText[0].Contains("LuaP")) {
                using (Process process = new Process()) {
                    process.StartInfo.FileName = "java.exe";
                    process.StartInfo.Arguments = $"-jar \"{Program.unlub}\" \"{_file}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;

                    StringBuilder outputBuilder = new StringBuilder();
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.WaitForExit();

                    File.WriteAllText(_file, outputBuilder.ToString());
                }
            }
        }
    }

    class TweakEngine
    {
        /// <summary>
        /// Apply the specified tweaks.
        /// </summary>
        /// <param name="rush">Required for status change</param>
        public static void ApplyTweaks(RushInterface rush) {
            string gameDirectory = Path.GetDirectoryName(Properties.Settings.Default.Path_GameDirectory);
            string[] files = Directory.GetFiles(gameDirectory, "*.arc", SearchOption.AllDirectories);
            string system = Literal.Core(Properties.Settings.Default.Path_GameDirectory);
            string tweak = string.Empty;

            // Define short variables to properties
            int renderer     = Properties.Settings.Default.Tweak_Renderer,
                reflections  = Properties.Settings.Default.Tweak_Reflections,
                antiAliasing = Properties.Settings.Default.Tweak_AntiAliasing,
                cameraType   = Properties.Settings.Default.Tweak_CameraType;

            decimal cameraHeight   = Properties.Settings.Default.Tweak_CameraHeight,
                    cameraDistance = Properties.Settings.Default.Tweak_CameraDistance,
                    hammerRange    = Properties.Settings.Default.Tweak_AmyHammerRange,
                    fieldOfView    = Properties.Settings.Default.Tweak_FieldOfView;

            bool forceMSAA        = Properties.Settings.Default.Tweak_ForceMSAA,
                 tailsFlightLimit = Properties.Settings.Default.Tweak_TailsFlightLimit;

            // Field of View
            if (fieldOfView != 90) {
                string xex = Path.Combine(gameDirectory, "default.xex"); // Location of the XEX

                if (!File.Exists($"{xex}_back"))
                    File.Copy(xex, $"{xex}_back", true);

                XEX.Decrypt(xex); // Decrypt the XEX to be able to modify it properly
                XEX.FieldOfView(xex, fieldOfView); // Set FOV
            }

            foreach (string archive in files) {
                if (Path.GetFileName(archive) == "cache.arc") {
                    int proceed = 0;

                    if (renderer != 0)         proceed++;
                    if (reflections != 1)      proceed++;
                    if (antiAliasing != 1)     proceed++;
                    if (!forceMSAA)            proceed++;
                    if (cameraType != 0)       proceed++;
                    if (cameraDistance != 650) proceed++;

                    if (proceed != 0) {
                        if (!File.Exists($"{archive}_back"))
                            File.Copy(archive, $"{archive}_back", true);

                        // Unpack archive to temporary location
                        tweak = ModEngine.UnpackARC(archive, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

                        // Default
                        if (renderer == 0) {
                            // Force MSAA
                            if (antiAliasing != 1 || forceMSAA) {
                                rush.Status = $"Tweaking Anti-Aliasing...";
                                MSAA(Path.Combine(tweak, $"cache\\{system}\\scripts\\render\\"), antiAliasing, SearchOption.TopDirectoryOnly);
                            }
                        }

                        // Optimised
                        else if (renderer == 1) {
                            rush.Status = $"Tweaking Renderer...";
                            File.WriteAllBytes(Path.Combine(tweak, $"cache\\{system}\\scripts\\render\\core\\render_main.lub"), Properties.Resources.optimised_render_main);
                        }

                        // Destructive
                        else if (renderer == 2) {
                            rush.Status = $"Tweaking Renderer...";
                            File.WriteAllBytes(Path.Combine(tweak, $"cache\\{system}\\scripts\\render\\render_gamemode.lub"),   Properties.Resources.vulkan_render_gamemode);
                            File.WriteAllBytes(Path.Combine(tweak, $"cache\\{system}\\scripts\\render\\render_title.lub"),      Properties.Resources.vulkan_render_title);
                            File.WriteAllBytes(Path.Combine(tweak, $"cache\\{system}\\scripts\\render\\core\\render_main.lub"), Properties.Resources.vulkan_render_main);
                        }

                        // Cheap
                        else if (renderer == 3) {
                            rush.Status = $"Tweaking Renderer...";
                            File.WriteAllBytes(Path.Combine(tweak, $"cache\\{system}\\scripts\\render\\render_gamemode.lub"), Properties.Resources.render_cheap);
                        }

                        // Reflections
                        if (reflections != 1) {
                            rush.Status = $"Tweaking Reflections...";
                            Reflections(Path.Combine(tweak, $"cache\\{system}\\scripts\\render\\core\\render_reflection.lub"), reflections);
                        }

                        if (system == "ps3") {
                            rush.Status = $"Tweaking Camera...";

                            // Camera Type
                            if (cameraType != 0)
                                CameraType(Path.Combine(tweak, $"cache\\{system}\\cameraparam.lub"), cameraType, fieldOfView);

                            // Camera Distance
                            if (cameraDistance != 650)
                                CameraDistance(Path.Combine(tweak, $"cache\\{system}\\cameraparam.lub"), (int)cameraDistance);
                        }

                        // Repack archive as tweak
                        ModEngine.RepackARC(tweak, archive);
                    }
                } else if (Path.GetFileName(archive) == "scripts.arc") {
                    int proceed = 0;

                    if (antiAliasing != 1)  proceed++;
                    if (!forceMSAA) proceed++;

                    if (proceed != 0) {
                        if (!File.Exists($"{archive}_back"))
                            File.Copy(archive, $"{archive}_back", true);

                        // Unpack archive to temporary location
                        tweak = ModEngine.UnpackARC(archive, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

                        // Default
                        if (Properties.Settings.Default.Tweak_Renderer == 0)
                            // Force MSAA
                            if (antiAliasing != 1 || forceMSAA) {
                                rush.Status = $"Tweaking Anti-Aliasing...";
                                MSAA(Path.Combine(tweak, $"scripts\\{system}\\scripts\\render\\"), antiAliasing, SearchOption.AllDirectories);
                            }

                        // Repack archive as tweak
                        ModEngine.RepackARC(tweak, archive);
                    }
                } else if (Path.GetFileName(archive) == "game.arc") {
                    int proceed = 0;

                    if (cameraType != 0)       proceed++;
                    if (cameraDistance != 650) proceed++;

                    if (proceed != 0) {
                        if (!File.Exists($"{archive}_back"))
                            File.Copy(archive, $"{archive}_back", true);

                        // Unpack archive to temporary location
                        tweak = ModEngine.UnpackARC(archive, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

                        if (system == "xenon") {
                            rush.Status = $"Tweaking Camera...";

                            // Camera Type
                            if (cameraType != 0)
                                CameraType(Path.Combine(tweak, $"game\\{system}\\cameraparam.lub"), cameraType, fieldOfView);

                            // Camera Distance
                            if (cameraDistance != 650)
                                CameraDistance(Path.Combine(tweak, $"game\\{system}\\cameraparam.lub"), (int)cameraDistance);
                        }

                        // Repack archive as tweak
                        ModEngine.RepackARC(tweak, archive);
                    }
                } else if (Path.GetFileName(archive) == "player.arc") {
                    int proceed = 0;

                    if (cameraHeight != 70) proceed++;
                    if (hammerRange != 50)  proceed++;
                    if (!tailsFlightLimit) proceed++;

                    if (proceed != 0) {
                        if (!File.Exists($"{archive}_back"))
                            File.Copy(archive, $"{archive}_back", true);

                        // Unpack archive to temporary location
                        tweak = ModEngine.UnpackARC(archive, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

                        // Tokyo Game Show
                        if (cameraType == 1) {
                            rush.Status = $"Tweaking Camera...";
                            CameraType(Path.Combine(tweak, $"player\\{system}\\player\\common.lub"), cameraType, fieldOfView);
                        }

                        // Camera Height
                        if (cameraHeight != 70) {
                            rush.Status = $"Tweaking Camera...";
                            CameraHeight(Path.Combine(tweak, $"player\\{system}\\player\\common.lub"), cameraHeight);
                        }

                        // Amy's Hammer Range
                        if (hammerRange != 50) {
                            rush.Status = $"Tweaking Characters...";
                            HammerRange(Path.Combine(tweak, $"player\\{system}\\player\\amy.lub"), hammerRange);
                        }

                        // Unlock Tails' Flight Limit
                        if (tailsFlightLimit) {
                            rush.Status = $"Tweaking Characters...";
                            UnlockTailsFlightLimit(Path.Combine(tweak, $"player\\{system}\\player\\tails.lub"));
                        }

                        // Repack archive as tweak
                        ModEngine.RepackARC(tweak, archive);
                    }
                }
            }
        }

        private static void MSAA(string directoryRoot, int MSAA, SearchOption searchOption) {
            string[] files = Directory.GetFiles(directoryRoot, "*.lub", searchOption);

            foreach (var lub in files) {
                PatchEngine.DecompileLua(lub);

                if (Path.GetFileName(lub) == "render_utility.lub") {
                    List<string> editedLua = File.ReadAllLines(lub).ToList();

                    if (MSAA == 0)      editedLua.Add("MSAAType = \"1x\"");
                    else if (MSAA == 1) editedLua.Add("MSAAType = \"2x\"");
                    else if (MSAA == 2) editedLua.Add("MSAAType = \"4x\"");
                    File.WriteAllLines(lub, editedLua);
                } else {
                    string[] editedLua = File.ReadAllLines(lub);
                    int lineNum = 0;
                    int modified = 0;

                    foreach (string line in editedLua) {
                        if (line.Contains("MSAAType")) {
                            string[] tempLine = line.Split(' ');
                            if (MSAA == 0)      tempLine[2] = "\"1x\"";
                            else if (MSAA == 1) tempLine[2] = "\"2x\"";
                            else if (MSAA == 2) tempLine[2] = "\"4x\"";
                            editedLua[lineNum] = string.Join(" ", tempLine);
                            modified++;
                        }
                        lineNum++;
                    }
                    if (modified != 0) File.WriteAllLines(lub, editedLua);
                }
            }
        }

        private static void Reflections(string directoryRoot, int scale) {
            PatchEngine.DecompileLua(directoryRoot);
            string[] editedLua = File.ReadAllLines(directoryRoot);
            int lineNum = 0;

            foreach (string line in editedLua) {
                if (line.StartsWith("EnableReflection")) {
                    string[] tempLine = line.Split(' ');
                    if (scale == 0)
                        tempLine[2] = "false";
                    else
                        tempLine[2] = "true";
                    editedLua[lineNum] = string.Join(" ", tempLine);
                }

                if (line.StartsWith("  texture_width") || line.StartsWith("  texture_height")) {
                    string[] tempLine = line.Split(' ');
                    if (scale == 1)
                        tempLine[7] = "4";
                    else if (scale == 2)
                        tempLine[7] = "2";
                    else if (scale == 3)
                        tempLine[6] = tempLine[7] = string.Empty;
                    editedLua[lineNum] = string.Join(" ", tempLine);
                }
                lineNum++;
            }
            File.WriteAllLines(directoryRoot, editedLua);
        }

        private static void CameraType(string directoryRoot, int type, decimal fov) {
            PatchEngine.DecompileLua(directoryRoot);
            string[] editedLua = File.ReadAllLines(directoryRoot);
            int lineNum = 0;

            foreach (string line in editedLua) {
                if (line.StartsWith("distance")) {
                    string[] tempLine = line.Split(' ');
                    if (type == 0)
                        tempLine[2] = "6.5"; //Retail
                    else if (type == 1)
                        if (fov > 90)
                            tempLine[2] = "3.5";
                        else
                            tempLine[2] = "4.5";
                    else if (type == 2)
                        tempLine[2] = "5.5"; //E3
                    editedLua[lineNum] = string.Join(" ", tempLine);
                }
                if (line.StartsWith("springK")) {
                    string[] tempLine = line.Split(' ');
                    if (type == 1)
                        if (fov > 90)
                            tempLine[2] = "0.325";
                        else
                            tempLine[2] = "0.225";
                    else
                        tempLine[2] = "0.98";
                    editedLua[lineNum] = string.Join(" ", tempLine);
                }
                if (line.StartsWith("altitude")) {
                    string[] tempLine = line.Split(' ');
                    if (type == 1)
                        tempLine[2] = "-15";
                    else
                        tempLine[2] = "15";
                    editedLua[lineNum] = string.Join(" ", tempLine);
                }
                if (line.StartsWith("az_driveK")) {
                    string[] tempLine = line.Split(' ');
                    if (type == 1)
                        tempLine[2] = "50000"; //TGS (32500 old)
                    else if(type == 2)
                        tempLine[2] = "690";
                    else
                        tempLine[2] = "3250";
                    editedLua[lineNum] = string.Join(" ", tempLine);
                }
                if (line.StartsWith("az_dampingK")) {
                    string[] tempLine = line.Split(' ');
                    if (type == 1)
                        tempLine[2] = "2500";
                    else if(type == 2)
                        tempLine[2] = "100";
                    else
                        tempLine[2] = "250";
                    editedLua[lineNum] = string.Join(" ", tempLine);
                }
                lineNum++;
            }
            File.WriteAllLines(directoryRoot, editedLua);
        }

        private static void CameraDistance(string directoryRoot, int distance) {
            PatchEngine.DecompileLua(directoryRoot);
            string[] editedLua = File.ReadAllLines(directoryRoot);
            int lineNum = 0;

            foreach (string line in editedLua) {
                if (line.StartsWith("distance")) {
                    string[] tempLine = line.Split(' ');
                    tempLine[2] = decimal.Divide(distance, 100).ToString();
                    editedLua[lineNum] = string.Join(" ", tempLine);
                }
                lineNum++;
            }
            File.WriteAllLines(directoryRoot, editedLua); //Resave the Lua
        }

        private static void CameraHeight(string directoryRoot, decimal height) {
            PatchEngine.DecompileLua(directoryRoot);
            string[] editedLua = File.ReadAllLines(directoryRoot);
            int lineNum = 0;

            foreach (string line in editedLua) {
                if (line.StartsWith("c_camera")) {
                    if (editedLua[lineNum].Contains("c_camera = { x ="))
                        editedLua[lineNum] = "c_camera = { x = 0 * meter, y = " + (height / 100) + " * meter, z = 0 * meter }";
                    else
                        editedLua[lineNum += 2] = $"  y = {height / 100} * meter,";
                }
                lineNum++;
            }
            File.WriteAllLines(directoryRoot, editedLua);
        }

        private static void HammerRange(string directoryRoot, decimal range) {
            PatchEngine.DecompileLua(directoryRoot);
            string[] editedLua = File.ReadAllLines(directoryRoot);
            int lineNum = 0;

            foreach (string line in editedLua) {
                string[] tempLine = line.Split(' ');
                if (line.StartsWith("c_hammer_head")) {
                    if (editedLua[lineNum].Contains("c_hammer_head"))
                        editedLua[lineNum] = $"c_hammer_head = {range / 100} * meter";
                }
                lineNum++;
            }
            File.WriteAllLines(directoryRoot, editedLua);
        }

        private static void UnlockTailsFlightLimit(string directoryRoot) {
            PatchEngine.DecompileLua(directoryRoot);
            string[] editedLua = File.ReadAllLines(directoryRoot);
            int lineNum = 0;
            decimal origTimer = 0;

            foreach (string line in editedLua) {
                string[] tempLine = line.Split(' ');

                if (tempLine[0] == "c_flight_timer") origTimer = decimal.Parse(tempLine[2]);

                if (tempLine[0] == "c_flight_timer_b") {
                    tempLine[2] = (((origTimer * 1000) + 125) / 1000).ToString();
                    editedLua[lineNum] = string.Join(" ", tempLine);
                }
                lineNum++;
            }
            File.WriteAllLines(directoryRoot, editedLua);
        }
    }

    class EBOOT
    {
        public static void Encrypt(string filepath) {
            string encryptedLocation = Path.ChangeExtension(filepath, "BIN_encrypt");

            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = Program.make_fself,
                Arguments = $"\"{filepath}\" \"{encryptedLocation}\""
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();

            if (File.Exists(encryptedLocation)) {
                File.Delete(filepath);
                File.Move(encryptedLocation, filepath);
            }
        }

        public static void Decrypt(string filepath) {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = Program.scetool,
                Arguments = $"-d \"{filepath}\" \"{filepath}\"",
                WorkingDirectory = Path.GetDirectoryName(Program.scetool)
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }
    }

    class XEX
    {
        public static void Encrypt(string filepath) {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = Program.XexTool,
                Arguments = $"-e e \"{filepath}\""
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }

        public static void Decrypt(string filepath) {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = Program.XexTool,
                Arguments = $"-e u \"{filepath}\""
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }

        public static void Decompress(string filepath) {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = Program.XexTool,
                Arguments = $"-c b \"{filepath}\""
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }

        public static void FieldOfView(string filepath, decimal fov) {
            using (var stream = File.Open(filepath, FileMode.Open, FileAccess.Write)) {
                stream.Position = 0x4F4D; stream.WriteByte(decimal.ToByte(fov));
            }
        }
    }

    class PKG
    {
        /// <summary>
        /// Use the PKGTool to encode/decode the given file.
        /// </summary>
        public static void PKGTool(string filepath) {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = Program.pkgtool,
                WorkingDirectory = Path.GetDirectoryName(Program.pkgtool),
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = filepath
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();

            // Erase the TXT file once encoded as PKG
            if (Path.GetExtension(filepath) == ".txt") File.Delete(filepath);
        }

        /// <summary>
        /// Adds an entry to a specified PKG - should be used for patches.
        /// </summary>
        public static void AddEntry(string filepath, string directoryRoot, string key, string _event, string reference) {
            // Backs up the archive containing the PKG
            if (!File.Exists($"{filepath}_back"))
                File.Copy(filepath, $"{filepath}_back", true);

            // Extracts the archive containing the PKG
            string unpack = ModEngine.UnpackARC(filepath, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

            // Decodes the PKG
            PKGTool($"{Path.Combine(unpack, directoryRoot)}.pkg");
            List<string> basePKG = File.ReadAllLines($"{Path.Combine(unpack, directoryRoot)}.txt").ToList();
            bool keyfound = false;
            int lineNum = 0;

            foreach (string line in basePKG) {
                // Look for the already existing key before adding a new one
                if (line.StartsWith($"\"{key}\"")) {
                    keyfound = true;
                    basePKG.Insert(lineNum + 2, $"\t\"{_event}\" = \"{reference}\";");
                    break;
                }
                lineNum++;
            }

            // Add new key to PKG if it doesn't exist already
            if (keyfound == false) {
                basePKG.Add($"\"{key}\"\n{"{"}");
                basePKG.Add($"\t\"{_event}\" = \"{reference}\";");
                basePKG.Add("}");
            }

            File.WriteAllLines($"{Path.Combine(unpack, directoryRoot)}.txt", basePKG); //Save the edited text file

            // Encodes the PKG
            PKGTool($"{Path.Combine(unpack, directoryRoot)}.txt");

            // Repacks the archive
            ModEngine.RepackARC(unpack, filepath);
        }
    }

    public static class ZIP
    {
        /// <summary>
        /// Extracts a ZIP file.
        /// </summary>
        public static void InstallFromZip(string ZipPath, string location) {
            try {
                if (File.Exists(ZipPath)) {
                    // Extracts all contents inside of the zip file
                    ZipFile.ExtractToDirectory(ZipPath, location);
                } else {
                    UnifyMessenger.UnifyMessage.ShowDialog($"Failed to extract '{Path.GetFileName(ZipPath)}'...",
                                                           "Extract failed...", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch { InstallFromCustomArchive(ZipPath, location); }
        }

        /// <summary>
        /// Extracts a ZIP file with extra parameters.
        /// </summary>
        public static void ExtractToDirectory(this ZipArchive archive, string destinationDirectoryName, bool overwrite) {
            if (!overwrite) {
                archive.ExtractToDirectory(destinationDirectoryName);
                return;
            }

            foreach (ZipArchiveEntry file in archive.Entries) {
                string completeFileName = Path.Combine(destinationDirectoryName, file.FullName);
                string directory = Path.GetDirectoryName(completeFileName);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (file.Name != "")
                    if (Path.GetFileName(completeFileName) != Path.GetFileName(Application.ExecutablePath))
                        file.ExtractToFile(completeFileName, true);
                    else
                        file.ExtractToFile(Path.Combine(destinationDirectoryName, $"{Application.ExecutablePath}.new"), true);
            }
        }

        /// <summary>
        /// Extracts 7Z/RAR files with 7-Zip.
        /// </summary>
        public static void InstallFromCustomArchive(string ArchivePath, string location) {
            if (File.Exists(ArchivePath)) {
                // Extracts the archive to the temp folder.
                Process process = new Process();
                ProcessStartInfo startInfo = new ProcessStartInfo {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = Program._7Zip,
                    Arguments = $"x -y -o\"{location}\" \"{ArchivePath}\""
                };
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            } else {
                UnifyMessenger.UnifyMessage.ShowDialog($"Failed to extract '{Path.GetFileName(ArchivePath)}'...",
                                                       "Extract failed...", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}