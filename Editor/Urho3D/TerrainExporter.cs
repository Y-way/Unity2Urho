﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityToCustomEngineExporter.Editor.Urho3D
{
    public class TerrainExporter
    {
        private const int _maxLayers = 5;
        private readonly Urho3DEngine _engine;

        public TerrainExporter(Urho3DEngine engine)
        {
            _engine = engine;
        }

        public static void WriteTgaHeader(BinaryWriter binaryWriter, int bitsPerPixel, int w, int h)
        {
            binaryWriter.Write((byte) 0);
            binaryWriter.Write((byte) 0);
            binaryWriter.Write((byte) (bitsPerPixel == 8 ? 3 : 2));
            binaryWriter.Write((short) 0);
            binaryWriter.Write((short) 0);
            binaryWriter.Write((byte) 0);
            binaryWriter.Write((short) 0);
            binaryWriter.Write((short) 0);
            binaryWriter.Write((short) w);
            binaryWriter.Write((short) h);
            binaryWriter.Write((byte) bitsPerPixel);
            binaryWriter.Write((byte) 0);
        }

        private static Color32 EncodeWeightsToColor(byte[] weights)
        {
            var color = new Color32(0, 0, 0, 255);
            if (weights.Length > 0)
            {
                color.r = weights[0];
                if (weights.Length > 1)
                {
                    color.g = weights[1];
                    if (weights.Length > 2)
                    {
                        color.b = weights[2];
                        if (weights.Length > 3) color.a = weights[3];
                    }
                }
            }

            return color;
        }

        private static void EncodeWeights(float[] weights, byte[] encodedWeights)
        {
            if (weights.Length == 0)
                return;
            var sum = weights.Sum();
            if (sum == 0)
            {
                weights[0] = 1;
                sum = 1;
            }

            sum /= 255.0f;

            var byteSum = 0;
            for (var index = 0; index < weights.Length; index++)
                byteSum += encodedWeights[index] = (byte) (weights[index] / sum);

            if (byteSum == 0) encodedWeights[0] = 255;
        }

        public string EvaluateHeightMap(TerrainData terrainData)
        {
            return ExportUtils.ReplaceExtension(ExportUtils.GetRelPathFromAsset(_engine.Options.Subfolder, terrainData),
                ".Heightmap.tga");
        }

        public string EvaluateWeightsMap(TerrainData terrainData)
        {
            return ExportUtils.ReplaceExtension(ExportUtils.GetRelPathFromAsset(_engine.Options.Subfolder, terrainData),
                ".Weights.tga");
        }

        public string EvaluateMaterial(TerrainData terrainData)
        {
            return ExportUtils.ReplaceExtension(ExportUtils.GetRelPathFromAsset(_engine.Options.Subfolder, terrainData),
                ".Material.xml");
        }

        public void ExportTerrain(TerrainData terrainData, PrefabContext prefabContext)
        {
            WriteTerrainMaterial(terrainData, prefabContext);
            WriteHeightMap(terrainData, prefabContext);
            WriteTerrainWeightsTexture(terrainData, prefabContext);
            ExportDetails(terrainData, prefabContext);
        }

        public int[] GetTerrainLayersByPopularity(TerrainData terrainData)
        {
            return GetTerrainLayersByPopularity(
                terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight),
                terrainData.terrainLayers.Length);
        }

        public int[] GetTerrainLayersByPopularity(float[,,] alphamaps, int layers)
        {
            var height = alphamaps.GetLength(0);
            var width = alphamaps.GetLength(1);
            layers = Math.Min(layers, alphamaps.GetLength(2));
            var weights = new double[layers];
            for (var y = height - 1; y >= 0; --y)
            for (var x = 0; x < width; ++x)
            for (var i = 0; i < layers; ++i)
                weights[i] += alphamaps[y, x, i];

            return weights.Select((w, i) => Tuple.Create(i, w)).OrderByDescending(_ => _.Item2).Select(_ => _.Item1)
                .ToArray();
        }

        private void ExportDetails(TerrainData terrainData, PrefabContext prefabContext)
        {
            //for (var detailIndex = 0; detailIndex < terrainData.detailPrototypes.Length; detailIndex++)
            //{
            //    var detailPrototype = terrainData.detailPrototypes[detailIndex];
            //    //detailPrototype.renderMode == DetailRenderMode.GrassBillboard

            //    //The Terrain system uses detail layer density maps. Each map is essentially a grayscale image where each pixel value denotes the number
            //    //of detail objects that will be procedurally placed terrain area. That corresponds to the pixel. Since several different detail types
            //    //may be used, the map is arranged into "layers" - the array indices of the layers are determined by the order of the detail types
            //    //defined in the Terrain inspector (ie, when the Paint Details tool is selected).
            //    var map = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight,
            //        detailIndex);
            //    for (var y = 0; y < terrainData.detailHeight; y++)
            //    for (var x = 0; x < terrainData.detailWidth; x++)
            //    {
            //        //The return value of each element [z,x] element is an int from 0-16, which // represent the number of details placed at that location. detailLayer[z,x]
            //        //So, if you want to set the number of flowers at this location to 8, just set it to 8. It would be the same as painting flowers there with the strength setting set to .5 (8 = 1/2 of 16). 
            //        var value = map[x, y];
            //    }
            //}
        }

        private void WriteTerrainMaterial(TerrainData terrain, PrefabContext prefabContext)
        {
            using (var writer =
                _engine.TryCreateXml(terrain.GetKey(), EvaluateMaterial(terrain),
                    ExportUtils.GetLastWriteTimeUtc(terrain)))
            {
                if (writer == null)
                    return;

                var layers = terrain.terrainLayers;
                var layerIndices = GetTerrainLayersByPopularity(terrain).Take(_maxLayers).ToArray();

                var material = new UrhoPBRMaterial();
                material.Technique = "Techniques/PBR/PBRTerrainBlend.xml";
                material.TextureUnits.Add(EvaluateWeightsMap(terrain));
                var detailTiling = new Vector2(1, 1);
                for (var layerIndex = 0; layerIndex < layerIndices.Length; ++layerIndex)
                {
                    var layer = layers[layerIndices[layerIndex]];
                    detailTiling = new Vector2(terrain.size.x / layer.tileSize.x, terrain.size.z / layer.tileSize.y);
                    if (layer.diffuseTexture != null)
                    {
                        _engine.ScheduleTexture(layer.diffuseTexture);
                        var urhoAssetName = _engine.EvaluateTextrueName(layer.diffuseTexture);
                        material.TextureUnits.Add(urhoAssetName);
                    }
                    else
                    {
                        material.TextureUnits.Add(null);
                    }
                }

                material.MatSpecColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                material.Roughness = 1;
                material.Metallic = 0;
                material.ExtraParameters.Add("DetailTiling", detailTiling);
                material.PixelShaderDefines.Add("TERRAINLAYERS" +
                                                layerIndices.Length.ToString(CultureInfo.InvariantCulture));

                AbstractMaterialExporter.WriteMaterial(writer, material, prefabContext);
            }
        }

        private void WriteHeightMap(TerrainData terrain, PrefabContext prefabContext)
        {
            using (var imageFile = _engine.TryCreate(terrain.GetKey(), EvaluateHeightMap(terrain), DateTime.MaxValue))
            {
                if (imageFile != null) SerializeHeightmapAsTga(imageFile, terrain);
            }
        }

        public static void SerializeHeightmapAsTga(Stream imageFile, TerrainData terrain)
        {
            var w = terrain.heightmapResolution;
            var h = terrain.heightmapResolution;
            var heights = terrain.GetHeights(0, 0, w, h);

            var max = float.MinValue;
            var min = float.MaxValue;

            foreach (var height in heights)
            {
                if (height > max) max = height;
                if (height < min) min = height;
            }

            if (max < min)
            {
                max = 1;
                min = 0;
            }
            else if (max == min)
            {
                max = min + 0.1f;
            }
            using (var binaryWriter = new BinaryWriter(imageFile))
            {
                WriteTgaHeader(binaryWriter, 32, w, h);
                for (var y = h - 1; y >= 0; --y)
                for (var x = 0; x < w; ++x)
                {
                    var height = (heights[h - y - 1, x] - min) / (max - min) * 255.0f;
                    var msb = (byte) height;
                    var lsb = (byte) ((height - msb) * 255.0f);
                    binaryWriter.Write((byte) 0); //B - none
                    binaryWriter.Write(lsb); //G - LSB
                    binaryWriter.Write(msb); //R - MSB
                    binaryWriter.Write((byte) 255); //A - none
                }
            }
        }

        private void WriteTerrainWeightsTexture(TerrainData terrain, PrefabContext prefabContext)
        {
            var layers = terrain.terrainLayers;
            var w = terrain.alphamapWidth;
            var h = terrain.alphamapHeight;
            var alphamaps = terrain.GetAlphamaps(0, 0, w, h);
            var numAlphamaps = alphamaps.GetLength(2);

            var layerIndices = GetTerrainLayersByPopularity(alphamaps, layers.Length);

            //Urho3D doesn't support more than 3 textures
            if (numAlphamaps > _maxLayers) numAlphamaps = _maxLayers;
            var weights = new float[numAlphamaps];
            var encodedWeights = new byte[numAlphamaps];
            using (var imageFile = _engine.TryCreate(terrain.GetKey(), EvaluateWeightsMap(terrain), DateTime.MaxValue))
            {
                if (imageFile == null)
                    return;

                using (var binaryWriter = new BinaryWriter(imageFile))
                {
                    var bytesPerPixell = 4;
                    WriteTgaHeader(binaryWriter, bytesPerPixell * 8, w, h);
                    for (var y = h - 1; y >= 0; --y)
                    for (var x = 0; x < w; ++x)
                    {
                        for (var i = 0; i < bytesPerPixell && i < layerIndices.Length; ++i)
                        {
                            var layer = layerIndices[i];
                            var weight = (byte) (alphamaps[h - y - 1, x, layer] * 255.0f);
                            weights[i] = weight;
                        }

                        EncodeWeights(weights, encodedWeights);

                        var color = EncodeWeightsToColor(encodedWeights);

                        binaryWriter.Write(color.b); //B
                        binaryWriter.Write(color.g); //G
                        binaryWriter.Write(color.r); //R
                        binaryWriter.Write(color.a); //A
                    }
                }
            }
        }
    }
}