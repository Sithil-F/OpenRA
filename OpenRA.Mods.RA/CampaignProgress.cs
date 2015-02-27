using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Graphics;

namespace OpenRA.Mods.RA
{
    public class CampaignProgress
    {
        private static string progressFile = Platform.ResolvePath("^", "cnc-progress.yaml");
        private static bool saveProgressFlag = false;
        private static bool gdiPathFlag = false;

        public static void Init()
        {
            ModMetadata initialMod = null;
            ModMetadata.AllMods.TryGetValue(Game.Settings.Game.Mod, out initialMod);
            string mod = initialMod.Id;
            progressFile = Platform.ResolvePath("^", mod + "-progress.yaml");
        }

        public static void SetGdiPathFlag()
        {
            gdiPathFlag = true;
        }

        public static void ResetGdiPathFlag()
        {
            gdiPathFlag = false;
        }

        public static void SetSaveProgressFlag()
        {
            saveProgressFlag = true;
        }

        public static void ResetSaveProgressFlag()
        {
            saveProgressFlag = false;
        }

        public static bool GetSaveProgressFlag()
        {
            return saveProgressFlag;
        }

        public static void SaveProgress(string faction, int mission)
        {
            if (saveProgressFlag)
            {
                if (!GlobalFileSystem.Exists(progressFile))
                    CreateProgressFile();
                var yaml = MiniYaml.FromFile(progressFile);
                foreach (var kv in yaml)
                {
                    if (kv.Key.Equals(faction))
                    {
                        foreach (var node in kv.Value.Nodes)
                        {
                            if (node.Key.Equals("Mission"))
                            {
                                node.Value.Value = mission.ToString();
                            }
                            else if (node.Key.Equals("Flag"))
                            {
                                node.Value.Value = gdiPathFlag ? "1" : "0";
                            }
                        }
                    }
                }

                yaml.WriteToFile(progressFile);
            }
        }

        public static int GetGdiProgress()
        {
            return GetMission("GDI");
        }

        public static int GetNodProgress()
        {
            return GetMission("Nod");
        }

        private static int GetMission(string faction)
        {
            if (!GlobalFileSystem.Exists(progressFile))
                CreateProgressFile();
            var yaml = MiniYaml.FromFile(progressFile);
            int mission = 0;
            foreach (var kv in yaml)
            {
                if (kv.Key.Equals(faction))
                {
                    foreach (var node in kv.Value.Nodes)
                    {
                        if (node.Key.Equals("Mission"))
                        {
                            mission = Convert.ToInt32(node.Value.Value);
                        }
                    }
                }
            }

            return mission;
        }

        public static int GetGdiPathFlag()
        {
            if (!GlobalFileSystem.Exists(progressFile))
                CreateProgressFile();
            var yaml = MiniYaml.FromFile(progressFile);
            int flag = 0;
            foreach (var kv in yaml)
            {
                if (kv.Key.Equals("GDI"))
                {
                    foreach (var node in kv.Value.Nodes)
                    {
                        if (node.Key.Equals("Flag"))
                        {
                            flag = Convert.ToInt32(node.Value.Value);
                        }
                    }
                }
            }

            return flag;
        }

        private static void CreateProgressFile()
        {
            List<MiniYamlNode> yaml = new List<MiniYamlNode>();
            List<MiniYamlNode> gdiNodes = new List<MiniYamlNode>();
            List<MiniYamlNode> nodNodes = new List<MiniYamlNode>();

            MiniYamlNode gdiMission = new MiniYamlNode("Mission", "0");
            MiniYamlNode gdiFlag = new MiniYamlNode("Flag", "0");
            gdiNodes.Add(gdiMission);
            gdiNodes.Add(gdiFlag);
            MiniYamlNode nodMission = new MiniYamlNode("Mission", "0");
            nodNodes.Add(nodMission);

            MiniYaml gdi = new MiniYaml(null, gdiNodes);
            MiniYamlNode gdiNode = new MiniYamlNode("GDI", gdi);
            MiniYaml nod = new MiniYaml(null, nodNodes);
            MiniYamlNode nodNode = new MiniYamlNode("Nod", nod);

            yaml.Add(gdiNode);
            yaml.Add(nodNode);

            yaml.WriteToFile(progressFile);
        }
    }
}