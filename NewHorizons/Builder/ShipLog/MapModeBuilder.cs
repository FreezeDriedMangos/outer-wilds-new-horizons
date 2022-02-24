﻿using NewHorizons.Components;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NewHorizons.External;
using NewHorizons.Utility;
using OWML.Common;
using UnityEngine;
using UnityEngine.UI;
using Logger = NewHorizons.Utility.Logger;
using NewHorizons.Builder.Handlers;
using System;

namespace NewHorizons.Builder.ShipLog
{
    public static class MapModeBuilder
    {
        #region General
        public static ShipLogAstroObject[][] ConstructMapMode(string systemName, GameObject transformParent, ShipLogAstroObject[][] currentNav, int layer)
        {
            Material greyScaleMaterial = GameObject.Find(ShipLogHandler.PAN_ROOT_PATH + "/TimberHearth/Sprite").GetComponent<Image>().material;
            List<NewHorizonsBody> bodies = Main.BodyDict[systemName].Where(
                b => (b.Config.ShipLog?.mapMode?.remove ?? false) == false
            ).ToList();
            bool flagManualPositionUsed = systemName == "SolarSystem";
            bool flagAutoPositionUsed = false;
            foreach (NewHorizonsBody body in bodies.Where(b => ShipLogHandler.IsVanillaBody(b) == false))
            {
                if (body.Config.ShipLog == null) continue;

                if (body.Config.ShipLog?.mapMode?.manualPosition == null)
                {
                    flagAutoPositionUsed = true;
                }
                else
                {
                    flagManualPositionUsed = true;
                    if (body.Config.ShipLog?.mapMode?.manualNavigationPosition == null)
                    {
                        Logger.LogError("Navigation position is missing for: " + body.Config.Name);
                        return null;
                    }
                }
            }

            if(flagManualPositionUsed)
            {
                if (flagAutoPositionUsed && flagManualPositionUsed)
                    Logger.LogWarning("Can't mix manual and automatic layout of ship log map mode, defaulting to manual");
                return ConstructMapModeManual(bodies, transformParent, greyScaleMaterial, currentNav, layer);
            }
            else if (flagAutoPositionUsed)
            {
                return ConstructMapModeAuto(bodies, transformParent, greyScaleMaterial, layer);
            }

            return null;
        }

        public static string GetAstroBodyShipLogName(string id)
        {
            return ShipLogHandler.GetNameFromAstroID(id) ?? id;
        }

        private static GameObject CreateImage(GameObject nodeGO, IModAssets assets, Texture2D texture, string name, int layer)
        {
            GameObject newImageGO = new GameObject(name);
            newImageGO.layer = layer;
            newImageGO.transform.SetParent(nodeGO.transform);

            RectTransform transform = newImageGO.AddComponent<RectTransform>();
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            Image newImage = newImageGO.AddComponent<Image>();

            Rect rect = new Rect(0, 0, texture.width, texture.height);
            Vector2 pivot = new Vector2(texture.width / 2, texture.height / 2);
            newImage.sprite = Sprite.Create(texture, rect, pivot);

            return newImageGO;
        }

        private static GameObject CreateMapModeGameObject(NewHorizonsBody body, GameObject parent, int layer, Vector2 position)
        {
            GameObject newGameObject = new GameObject(body.Config.Name + "_ShipLog");
            newGameObject.layer = layer;
            newGameObject.transform.SetParent(parent.transform);

            RectTransform transform = newGameObject.AddComponent<RectTransform>();
            float scale = body.Config.ShipLog?.mapMode?.scale ?? 1f;
            transform.localPosition = position;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * scale;
            transform.SetAsFirstSibling();
            return newGameObject;
        }

        private static ShipLogAstroObject AddShipLogAstroObject(GameObject gameObject, NewHorizonsBody body, Material greyScaleMaterial, int layer)
        {
            const float unviewedIconOffset = 15;
            
            GameObject unviewedReference = GameObject.Find(ShipLogHandler.PAN_ROOT_PATH + "/TimberHearth/UnviewedIcon");
            
            ShipLogAstroObject astroObject = gameObject.AddComponent<ShipLogAstroObject>();
            astroObject._id = ShipLogHandler.GetAstroObjectId(body);

            Texture2D image;
            Texture2D outline;
            
            string imagePath = body.Config.ShipLog?.mapMode?.revealedSprite;
            string outlinePath = body.Config.ShipLog?.mapMode?.outlineSprite;

            if (imagePath != null) image = body.Mod.Assets.GetTexture(imagePath);
            else image = AutoGenerateMapModePicture(body);

            if (outlinePath != null) outline = body.Mod.Assets.GetTexture(outlinePath);
            else outline = ImageUtilities.MakeOutline(image, Color.white, 10);

            astroObject._imageObj = CreateImage(gameObject, body.Mod.Assets, image, body.Config.Name + " Revealed", layer);
            astroObject._outlineObj = CreateImage(gameObject, body.Mod.Assets, outline, body.Config.Name + " Outline", layer);
            if (ShipLogHandler.BodyHasEntries(body))
            {
                Image revealedImage = astroObject._imageObj.GetComponent<Image>();
                astroObject._greyscaleMaterial = greyScaleMaterial;
                revealedImage.material = greyScaleMaterial;
                revealedImage.color = Color.white;
                astroObject._image = revealedImage;
            }

            astroObject._unviewedObj = GameObject.Instantiate(unviewedReference, gameObject.transform, false);
            astroObject._invisibleWhenHidden = body.Config.ShipLog?.mapMode?.invisibleWhenHidden ?? false;

            Rect imageRect = astroObject._imageObj.GetComponent<RectTransform>().rect;
            astroObject._unviewedObj.transform.localPosition = new Vector3(imageRect.width / 2 + unviewedIconOffset, imageRect.height / 2 + unviewedIconOffset, 0);
            return astroObject;
        }
        #endregion
        
        # region Details
        private static void MakeDetail(ShipLogModule.ShipLogDetailInfo info, Transform parent, NewHorizonsBody body, Material greyScaleMaterial)
        {
            GameObject detailGameObject = new GameObject("Detail");
            detailGameObject.transform.SetParent(parent);
            detailGameObject.SetActive(false);

            RectTransform detailTransform = detailGameObject.AddComponent<RectTransform>();
            detailTransform.localPosition = (Vector2)(info.position ?? new MVector2(0, 0));
            detailTransform.localRotation = Quaternion.Euler(0f, 0f, info.rotation);
            detailTransform.localScale = (Vector2)(info.scale ?? new MVector2(0, 0));

            Texture2D image;
            Texture2D outline;

            string imagePath = info.revealedSprite;
            string outlinePath = info.outlineSprite;

            if (imagePath != null) image = body.Mod.Assets.GetTexture(imagePath);
            else image = AutoGenerateMapModePicture(body);

            if (outlinePath != null) outline = body.Mod.Assets.GetTexture(outlinePath);
            else outline = ImageUtilities.MakeOutline(image, Color.white, 10);

            Image revealedImage = CreateImage(detailGameObject, body.Mod.Assets, image, "Detail Revealed", parent.gameObject.layer).GetComponent<Image>();
            Image outlineImage = CreateImage(detailGameObject, body.Mod.Assets, outline, "Detail Outline", parent.gameObject.layer).GetComponent<Image>();

            ShipLogDetail detail = detailGameObject.AddComponent<ShipLogDetail>();
            detail.Init(info, revealedImage, outlineImage, greyScaleMaterial);
            detailGameObject.SetActive(true);
        }

        private static void MakeDetails(NewHorizonsBody body, Transform parent, Material greyScaleMaterial)
        {
            if (body.Config.ShipLog?.mapMode?.details?.Length > 0)
            {
                GameObject detailsParent = new GameObject("Details");
                detailsParent.transform.SetParent(parent);
                detailsParent.SetActive(false);

                RectTransform detailsTransform = detailsParent.AddComponent<RectTransform>();
                detailsTransform.localPosition = Vector3.zero;
                detailsTransform.localRotation = Quaternion.identity;
                detailsTransform.localScale = Vector3.one;

                foreach (ShipLogModule.ShipLogDetailInfo detailInfo in body.Config.ShipLog.mapMode.details)
                {
                    MakeDetail(detailInfo, detailsTransform, body, greyScaleMaterial);
                }
                detailsParent.SetActive(true);
            }
        }
        #endregion

        #region Manual Map Mode
        private static ShipLogAstroObject[][] ConstructMapModeManual(List<NewHorizonsBody> bodies, GameObject transformParent, Material greyScaleMaterial, ShipLogAstroObject[][] currentNav, int layer)
        {
            int maxAmount = bodies.Count + 20;
            ShipLogAstroObject[][] navMatrix = new ShipLogAstroObject[maxAmount][];
            for (int i = 0; i < maxAmount; i++)
            {
                navMatrix[i] = new ShipLogAstroObject[maxAmount];
            }

            Dictionary<string, int[]> astroIdToNavIndex = new Dictionary<string, int[]>();

            if (Main.Instance.CurrentStarSystem == "SolarSystem")
            {
                
                for (int y = 0; y < currentNav.Length; y++)
                {
                    for (int x = 0; x < currentNav[y].Length; x++)
                    {
                        navMatrix[y][x] = currentNav[y][x];
                        astroIdToNavIndex.Add(currentNav[y][x].GetID(), new [] {y, x});
                    }
                }                        
            }

            foreach(NewHorizonsBody body in bodies)
            {
                if (body.Config.ShipLog?.mapMode?.manualNavigationPosition == null) continue;

                // Sometimes they got other names idk
                var name = body.Config.Name.Replace(" ", "");
                var existingBody = AstroObjectLocator.GetAstroObject(body.Config.Name);
                if (existingBody != null)
                {
                    var astroName = existingBody.GetAstroObjectName();
                    if (astroName == AstroObject.Name.RingWorld) name = "InvisiblePlanet";
                    else if (astroName != AstroObject.Name.CustomString) name = astroName.ToString();
                }
                // Should probably also just fix the IsVanilla method
                var isVanilla = ShipLogHandler.IsVanillaBody(body);

                if (!isVanilla)
                {
                    GameObject newMapModeGO = CreateMapModeGameObject(body, transformParent, layer, body.Config.ShipLog?.mapMode?.manualPosition);
                    ShipLogAstroObject newAstroObject = AddShipLogAstroObject(newMapModeGO, body, greyScaleMaterial, layer);
                    MakeDetails(body, newMapModeGO.transform, greyScaleMaterial);
                    Vector2 navigationPosition = body.Config.ShipLog?.mapMode?.manualNavigationPosition;
                    navMatrix[(int)navigationPosition.y][(int)navigationPosition.x] = newAstroObject;
                }
                else if (Main.Instance.CurrentStarSystem == "SolarSystem")
                {
                    GameObject gameObject = GameObject.Find(ShipLogHandler.PAN_ROOT_PATH + "/" + name);
                    if (body.Config.Destroy || (body.Config.ShipLog?.mapMode?.remove ?? false))
                    {
                        ShipLogAstroObject astroObject = gameObject.GetComponent<ShipLogAstroObject>();
                        if (astroObject != null)
                        {
                            int[] navIndex = astroIdToNavIndex[astroObject.GetID()];
                            navMatrix[navIndex[0]][navIndex[1]] = null;
                            if (astroObject.GetID() == "CAVE_TWIN" || astroObject.GetID() == "TOWER_TWIN")
                            {
                                GameObject.Find(ShipLogHandler.PAN_ROOT_PATH + "/" + "SandFunnel").SetActive(false);
                            }
                        }
                        else if (name == "SandFunnel")
                        {
                            GameObject.Find(ShipLogHandler.PAN_ROOT_PATH + "/" + "SandFunnel").SetActive(false);
                        }
                        gameObject.SetActive(false);
                    }
                    else
                    {
                        if (body.Config.ShipLog?.mapMode?.manualPosition != null)
                        {
                            gameObject.transform.localPosition = (Vector2)body.Config.ShipLog.mapMode.manualPosition;
                        }
                        if (body.Config.ShipLog?.mapMode?.manualNavigationPosition != null)
                        {
                            Vector2 navigationPosition = body.Config.ShipLog?.mapMode?.manualNavigationPosition;
                            navMatrix[(int)navigationPosition.y][(int)navigationPosition.x] = gameObject.GetComponent<ShipLogAstroObject>();
                        }
                        if (body.Config.ShipLog?.mapMode?.scale != null)
                        {
                            gameObject.transform.localScale = Vector3.one * body.Config.ShipLog.mapMode.scale;
                        }
                    }
                }
            }

            navMatrix = navMatrix.Where(a => a.Count(c => c != null && c.gameObject != null) > 0).Prepend(new ShipLogAstroObject[1]).ToArray();
            for (var index = 0; index < navMatrix.Length; index++)
            {
                navMatrix[index] = navMatrix[index].Where(a => a != null && a.gameObject != null).ToArray();
            }

            return navMatrix;
        }
        #endregion
        
        #region Automatic Map Mode
        private class MapModeObject
        {
            public int x;
            public int y;
            public int branch_width;
            public int branch_height;
            public int level;
            public NewHorizonsBody mainBody;
            public ShipLogAstroObject astroObject;
            public List<MapModeObject> children;
            public MapModeObject parent;
            public MapModeObject lastSibling;
            public void Increment_width()
            {
                branch_width++;
                parent?.Increment_width();
            }
            public void Increment_height()
            {
                branch_height++;
                parent?.Increment_height();
            }
        }
        
        private static ShipLogAstroObject[][] ConstructMapModeAuto(List<NewHorizonsBody> bodies, GameObject transformParent, Material greyScaleMaterial, int layer)
        {
            MapModeObject rootObject = ConstructPrimaryNode(bodies);
            if (rootObject.mainBody != null)
            {
                MakeAllNodes(ref rootObject, transformParent, greyScaleMaterial, layer);
            }

            int maxAmount = bodies.Count;
            ShipLogAstroObject[][] navMatrix = new ShipLogAstroObject[maxAmount][];
            for (int i = 0; i < maxAmount; i++)
            {                                                                          
                navMatrix[i] = new ShipLogAstroObject[maxAmount];
            }

            CreateNavigationMatrix(rootObject, ref navMatrix);
            navMatrix = navMatrix.Where(a => a.Count(c => c != null) > 0).Prepend(new ShipLogAstroObject[1]).ToArray();
            for (var index = 0; index < navMatrix.Length; index++)
            {
                navMatrix[index] = navMatrix[index].Where(a => a != null).ToArray();
            }
            return navMatrix;
        }

        private static void CreateNavigationMatrix(MapModeObject root, ref ShipLogAstroObject[][] navMatrix)
        {
            if (root.astroObject != null)
            {
                navMatrix[root.y][root.x] = root.astroObject;
            }
            foreach (MapModeObject child in root.children)
            {
                CreateNavigationMatrix(child, ref navMatrix);
            }
        }

        private static void MakeAllNodes(ref MapModeObject parentNode, GameObject parent, Material greyScaleMaterial, int layer)
        {
            MakeNode(ref parentNode, parent, greyScaleMaterial, layer);
            for (var i = 0; i < parentNode.children.Count; i++)
            {
                MapModeObject child = parentNode.children[i];
                MakeAllNodes(ref child, parent, greyScaleMaterial, layer);
                parentNode.children[i] = child;
            }
        }
        
        private static MapModeObject ConstructPrimaryNode(List<NewHorizonsBody> bodies)
        {
            foreach (NewHorizonsBody body in bodies.Where(b => b.Config.Base.CenterOfSolarSystem))
            {
                bodies.Sort((b, o) => b.Config.Orbit.SemiMajorAxis.CompareTo(o.Config.Orbit.SemiMajorAxis));
                MapModeObject newNode = new MapModeObject
                {
                    mainBody = body,
                    level = 0,
                    x = 0,
                    y = 0
                };
                newNode.children = ConstructChildrenNodes(newNode, bodies);
                return newNode;
            }
            Logger.LogError("Couldn't find center of system!");
            return new MapModeObject();
        }

        private static List<MapModeObject> ConstructChildrenNodes(MapModeObject parent, List<NewHorizonsBody> searchList, string secondaryName = "")
        {
            List<MapModeObject> children = new List<MapModeObject>();
            int newX = parent.x;
            int newY = parent.y;
            int newLevel = parent.level + 1;
            MapModeObject lastSibling = parent;
            
            foreach (NewHorizonsBody body in searchList.Where(b => b.Config.Orbit.PrimaryBody == parent.mainBody.Config.Name || b.Config.Name == secondaryName))
            {
                bool even = newLevel % 2 == 0;
                newX = even ? newX : newX + 1;
                newY = even ? newY + 1 : newY;
                MapModeObject newNode = new MapModeObject()
                {
                    mainBody = body,
                    level = newLevel,
                    x = newX,
                    y = newY,
                    parent = parent,
                    lastSibling = lastSibling
                };
                string newSecondaryName = "";
                if (body.Config.FocalPoint != null)
                {
                    newNode.mainBody = searchList.Find(b => b.Config.Name == body.Config.FocalPoint.Primary);
                    newSecondaryName = searchList.Find(b => b.Config.Name == body.Config.FocalPoint.Secondary).Config.Name;
                }

                newNode.children = ConstructChildrenNodes(newNode, searchList, newSecondaryName);
                if (even)
                {
                    newY += newNode.branch_height;
                    parent.Increment_height();
                }
                else
                {
                    newX += newNode.branch_width;
                    parent.Increment_width();
                }

                lastSibling = newNode;
                children.Add(newNode);
            }
            return children;
        }
        
        private static void ConnectNodeToLastSibling(MapModeObject node, Material greyScaleMaterial)
        {
            Vector2 fromPosition = node.astroObject.transform.localPosition;
            Vector2 toPosition = node.lastSibling.astroObject.transform.localPosition;

            GameObject newLink = new GameObject("Line_ShipLog");
            newLink.layer = node.astroObject.gameObject.layer;
            newLink.SetActive(false);

            RectTransform transform = newLink.AddComponent<RectTransform>();
            transform.SetParent(node.astroObject.transform.parent);
            Vector2 center = toPosition + (fromPosition - toPosition) / 2;
            transform.localPosition = new Vector3(center.x, center.y, -1);
            transform.localRotation = Quaternion.identity;
            transform.localScale = node.level % 2 == 0 ? new Vector3(node.astroObject.transform.localScale.x / 5f, Mathf.Abs(fromPosition.y - toPosition.y) / 100f, 1) : new Vector3(Mathf.Abs(fromPosition.x - toPosition.x) / 100f, node.astroObject.transform.localScale.y / 5f, 1);
            Image linkImage = newLink.AddComponent<Image>();
            linkImage.color = new Color(0.28f, 0.28f, 0.5f, 0.12f);

            ShipLogModule.ShipLogDetailInfo linkDetailInfo = new ShipLogModule.ShipLogDetailInfo()
            {
                invisibleWhenHidden = node.mainBody.Config.ShipLog?.mapMode?.invisibleWhenHidden ?? false
            };

            ShipLogDetail linkDetail = newLink.AddComponent<ShipLogDetail>();
            linkDetail.Init(linkDetailInfo, linkImage, linkImage, greyScaleMaterial);

            transform.SetParent(node.astroObject.transform);
            transform.SetAsFirstSibling();
            newLink.SetActive(true);
        }

        private static void MakeNode(ref MapModeObject node, GameObject parent, Material greyScaleMaterial, int layer)
        {
            const float padding = 100f;
            Vector2 position = Vector2.zero;
            if (node.lastSibling != null)
            {
                ShipLogAstroObject lastAstroObject = node.lastSibling.astroObject;
                Vector3 lastPosition = lastAstroObject.transform.localPosition;
                position = lastPosition;
                float extraDistance = (node.mainBody.Config.ShipLog?.mapMode?.offset ?? 0f) * 100;

                if (node.level % 2 == 0)
                {
                    position.y += padding * (node.y - node.lastSibling.y) + extraDistance;
                }
                else
                {
                    position.x += padding * (node.x - node.lastSibling.x) + extraDistance;
                }
            }
            GameObject newNodeGO = CreateMapModeGameObject(node.mainBody, parent, layer, position);
            ShipLogAstroObject astroObject = AddShipLogAstroObject(newNodeGO, node.mainBody, greyScaleMaterial, layer);
            if (node.mainBody.Config.FocalPoint != null)
            {
                astroObject._imageObj.GetComponent<Image>().enabled = false;
                astroObject._outlineObj.GetComponent<Image>().enabled = false;
                astroObject._unviewedObj.GetComponent<Image>().enabled = false;
                astroObject.transform.localScale = node.lastSibling.astroObject.transform.localScale;
            }
            node.astroObject = astroObject;
            if (node.lastSibling != null) ConnectNodeToLastSibling(node, greyScaleMaterial);
            MakeDetails(node.mainBody, newNodeGO.transform, greyScaleMaterial);
        }
        #endregion
    
        private static Texture2D AutoGenerateMapModePicture(NewHorizonsBody body)
        {
            Texture2D texture;

            if(body.Config.Star != null) texture = Main.Instance.ModHelper.Assets.GetTexture("AssetBundle/DefaultMapModeStar.png");
            else if(body.Config.Atmosphere != null) texture = Main.Instance.ModHelper.Assets.GetTexture("AssetBundle/DefaultMapModNoAtmo.png");
            else texture = Main.Instance.ModHelper.Assets.GetTexture("AssetBundle/DefaultMapModePlanet.png");

            var color = GetDominantPlanetColor(body);
            var darkColor = new Color(color.r / 3f, color.g / 3f, color.b / 3f);

            texture = ImageUtilities.LerpGreyscaleImage(texture, color, darkColor);

            return texture;
        }

        private static Color GetDominantPlanetColor(NewHorizonsBody body)
        {
            try
            {
                if (body.Config?.Singularity?.Type == "BlackHole") return Color.black;
                if (body.Config?.Singularity?.Type == "WhiteHole") return Color.white;

                var starColor = body.Config?.Star?.Tint;
                if (starColor != null) return starColor.ToColor();

                var atmoColor = body.Config.Atmosphere?.AtmosphereTint;
                if (body.Config.Atmosphere?.Cloud != null && atmoColor != null) return atmoColor.ToColor();

                if (body.Config?.HeightMap?.TextureMap != null)
                {
                    try
                    {
                        var texture = body.Mod.Assets.GetTexture(body.Config.HeightMap.TextureMap);
                        var landColor = ImageUtilities.GetAverageColor(texture);
                        if (landColor != null) return landColor;
                    }
                    catch (Exception) { }
                }

                var waterColor = body.Config.Water?.Tint;
                if (waterColor != null) return waterColor.ToColor();

                var lavaColor = body.Config.Lava?.Tint;
                if (lavaColor != null) return lavaColor.ToColor();

                var sandColor = body.Config.Sand?.Tint;
                if (sandColor != null) return sandColor.ToColor();
            }
            catch(Exception)
            {
                Logger.LogWarning($"Something went wrong trying to pick the colour for {body.Config.Name} but I'm too lazy to fix it.");
            }

            return Color.white;
        }
    }
}