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

        public static void init()
        {
            ModMetadata initialMod = null;
            ModMetadata.AllMods.TryGetValue(Game.Settings.Game.Mod, out initialMod);
            string mod = initialMod.Id;
            progressFile = Platform.ResolvePath("^", mod + "-progress.yaml");
        }

        public static void setGdiPathFlag()
        {
            gdiPathFlag = true;
        }

        public static void resetGdiPathFlag()
        {
            gdiPathFlag = false;
        }

        public static void setSaveProgressFlag()
        {
            saveProgressFlag = true;
        }

        public static void resetSaveProgressFlag()
        {
            saveProgressFlag = false;
        }

        public static bool getSaveProgressFlag()
        {
            return saveProgressFlag;
        }

        public static void saveProgress(string faction, int mission)
        {
            if (saveProgressFlag)
            {
                if (!GlobalFileSystem.Exists(progressFile))
                    createProgressFile();
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

        public static int getGdiProgress()
        {
            return getMission("GDI");
        }

        public static int getNodProgress()
        {
            return getMission("Nod");
        }

        private static int getMission(string faction)
        {
            if (!GlobalFileSystem.Exists(progressFile))
                createProgressFile();
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

        public static int getGdiPathFlag()
        {
            if (!GlobalFileSystem.Exists(progressFile))
                createProgressFile();
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

        private static void createProgressFile()
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