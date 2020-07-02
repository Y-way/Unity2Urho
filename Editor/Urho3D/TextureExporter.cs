﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityToCustomEngineExporter.Editor.Urho3D
{
    public class TextureExporter
    {
        private readonly Urho3DEngine _engine;

        public TextureExporter(Urho3DEngine engine)
        {
            _engine = engine;
        }

        private static string GetTextureOutputName(string baseAssetName, TextureReference reference)
        {
            switch (reference.Semantic)
            {
                case TextureSemantic.PBRMetallicGlossiness:
                    return ExportUtils.ReplaceExtension(baseAssetName, ".MetallicRoughness.png");
                case TextureSemantic.PBRSpecularGlossiness:
                    return ExportUtils.ReplaceExtension(baseAssetName, ".MetallicRoughness.png");
                case TextureSemantic.PBRDiffuse:
                    return ExportUtils.ReplaceExtension(baseAssetName, ".BaseColor.png");
                default: return baseAssetName;
            }
        }

        private static void DestroyTmpTexture(TextureOrColor reference, Texture specularTexture)
        {
            if (specularTexture != null && specularTexture != reference.Texture)
                Object.DestroyImmediate(specularTexture);
        }

        private static Texture EnsureTexture(TextureOrColor textureOrColor)
        {
            var specularTexture = textureOrColor.Texture;
            if (specularTexture == null)
            {
                var tmpSpecularTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                tmpSpecularTexture.SetPixels(new[] {textureOrColor.Color});
                tmpSpecularTexture.Apply();
                return tmpSpecularTexture;
            }

            return specularTexture;
        }

        public void WriteOptions(string urhoTextureName, DateTime lastWriteTimeUtc, TextureOptions options)
        {
            if (options == null)
                return;
            var xmlFileName =ExportUtils.ReplaceExtension(urhoTextureName, ".xml");
            if (xmlFileName == urhoTextureName)
                return;
            using (var writer = _engine.TryCreateXml(xmlFileName, lastWriteTimeUtc))
            {
                if (writer != null)
                {
                    writer.WriteStartElement("texture");
                    writer.WriteWhitespace(Environment.NewLine);
                    switch (options.filterMode)
                    {
                        case FilterMode.Point:
                            writer.WriteElementParameter("filter", "mode", "nearest");
                            break;
                        case FilterMode.Bilinear:
                            writer.WriteElementParameter("filter", "mode", "bilinear");
                            break;
                        case FilterMode.Trilinear:
                            writer.WriteElementParameter("filter", "mode", "trilinear");
                            break;
                        default:
                            writer.WriteElementParameter("filter", "mode", "default");
                            break;
                    }

                    switch (options.wrapMode)
                    {
                        case TextureWrapMode.Repeat:
                            writer.WriteElementParameter("address", "mode", "wrap");
                            break;
                        case TextureWrapMode.Clamp:
                            writer.WriteElementParameter("address", "mode", "clamp");
                            break;
                        case TextureWrapMode.Mirror:
                            writer.WriteElementParameter("address", "mode", "mirror");
                            break;
                    }
                    writer.WriteElementParameter("srgb", "enable", options.sRGBTexture? "true": "false");
                    writer.WriteElementParameter("mipmap", "enable", options.mipmapEnabled ? "true" : "false");
                    writer.WriteEndElement();
                }
            }
        }

        public void ExportTexture(Texture texture, TextureReference textureReference)
        {
            if (textureReference == null)
            {
                CopyTexture(texture);
                return;
            }

            switch (textureReference.Semantic)
            {
                case TextureSemantic.PBRMetallicGlossiness:
                {
                    TransformMetallicGlossiness(texture, (PBRMetallicGlossinessTextureReference) textureReference);
                    break;
                }
                case TextureSemantic.PBRSpecularGlossiness:
                {
                    TransformSpecularGlossiness(texture, (PBRSpecularGlossinessTextureReference) textureReference);
                    break;
                }
                case TextureSemantic.PBRDiffuse:
                {
                    TransformDiffuse(texture, (PBRDiffuseTextureReference) textureReference);
                    break;
                }
                default:
                {
                    CopyTexture(texture);
                    break;
                }
            }
        }

        public string EvaluateTextrueName(Texture texture, TextureReference reference)
        {
            var baseName = EvaluateTextrueName(texture);
            return GetTextureOutputName(baseName, reference);
        }

        public string EvaluateTextrueName(Texture texture)
        {
            if (texture == null)
                return null;
            var assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            var newExt = Path.GetExtension(assetPath);
            if (texture is Cubemap)
                newExt = ".xml";
            else
                switch (newExt)
                {
                    case ".tif":
                        newExt = ".png";
                        break;
                }

            return ExportUtils.ReplaceExtension(ExportUtils.GetRelPathFromAssetPath(assetPath), newExt);
        }

        private void CopyTexture(Texture texture)
        {
            var relPath = ExportUtils.GetRelPathFromAsset(texture);
            var newName = EvaluateTextrueName(texture);
            if (relPath != newName)
            {
                CopyTextureAsPng(texture);
            }
            else
            {
                _engine.TryCopyFile(AssetDatabase.GetAssetPath(texture), newName);
                WriteOptions(newName, ExportUtils.GetLastWriteTimeUtc(texture), ExportUtils.GetTextureOptions(texture));
            }
        }

        private void CopyTextureAsPng(Texture texture)
        {
            var outputAssetName = EvaluateTextrueName(texture);
            var sourceFileTimestampUtc = ExportUtils.GetLastWriteTimeUtc(texture);
            if (_engine.IsUpToDate(outputAssetName, sourceFileTimestampUtc)) return;

            var tImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            var texType = tImporter?.textureType ?? TextureImporterType.Default;
            switch (texType)
            {
                case TextureImporterType.NormalMap:
                    new TextureProcessor().ProcessAndSaveTexture(texture,
                        "Hidden/UnityToCustomEngineExporter/Urho3D/DecodeNormalMap",
                        _engine.GetTargetFilePath(outputAssetName));
                    break;
                default:
                    new TextureProcessor().ProcessAndSaveTexture(texture,
                        "Hidden/UnityToCustomEngineExporter/Urho3D/Copy", _engine.GetTargetFilePath(outputAssetName));
                    WriteOptions(outputAssetName, sourceFileTimestampUtc, ExportUtils.GetTextureOptions(texture).WithSRGB(true));
                    break;
            }
        }

        private void TransformDiffuse(Texture texture, PBRDiffuseTextureReference reference)
        {
            var baseColorName = GetTextureOutputName(EvaluateTextrueName(texture), reference);
            var sourceFileTimestampUtc = reference.GetLastWriteTimeUtc(texture);
            if (_engine.IsUpToDate(baseColorName, sourceFileTimestampUtc)) return;

            var tmpMaterial = new Material(Shader.Find("Hidden/UnityToCustomEngineExporter/Urho3D/ConvertToBaseColor"));
            Texture specularTexture = null;
            Texture smoothnessTexture = null;
            try
            {
                tmpMaterial.SetTexture("_MainTex", texture);
                specularTexture = EnsureTexture(reference.Specular);
                tmpMaterial.SetTexture("_SpecGlossMap", specularTexture);
                tmpMaterial.SetFloat("_SmoothnessScale", reference.SmoothnessScale);
                smoothnessTexture = EnsureTexture(reference.Smoothness);
                tmpMaterial.SetTexture("_Smoothness", smoothnessTexture);
                new TextureProcessor().ProcessAndSaveTexture(texture, tmpMaterial,
                    _engine.GetTargetFilePath(baseColorName));
                WriteOptions(baseColorName, sourceFileTimestampUtc, ExportUtils.GetTextureOptions(texture).WithSRGB(true));
            }
            finally
            {
                Object.DestroyImmediate(tmpMaterial);
                DestroyTmpTexture(reference.Specular, specularTexture);
                DestroyTmpTexture(reference.Smoothness, smoothnessTexture);
            }
        }

        private void TransformMetallicGlossiness(Texture texture, PBRMetallicGlossinessTextureReference reference)
        {
            var baseColorName = GetTextureOutputName(EvaluateTextrueName(texture), reference);
            if (_engine.IsUpToDate(baseColorName, reference.GetLastWriteTimeUtc(texture))) return;

            var tmpMaterial =
                new Material(Shader.Find("Hidden/UnityToCustomEngineExporter/Urho3D/ConvertToMetallicRoughness"));
            tmpMaterial.SetTexture("_MainTex", texture);
            tmpMaterial.SetFloat("_SmoothnessScale", reference.SmoothnessScale);
            tmpMaterial.SetTexture("_Smoothness", reference.Smoothness);
            try
            {
                new TextureProcessor().ProcessAndSaveTexture(texture, tmpMaterial,
                    _engine.GetTargetFilePath(baseColorName));
            }
            finally
            {
                Object.DestroyImmediate(tmpMaterial);
            }
        }

        private void TransformSpecularGlossiness(Texture texture, PBRSpecularGlossinessTextureReference reference)
        {
            var baseColorName = GetTextureOutputName(EvaluateTextrueName(texture), reference);
            if (_engine.IsUpToDate(baseColorName, reference.GetLastWriteTimeUtc(texture))) return;

            var tmpMaterial =
                new Material(
                    Shader.Find("Hidden/UnityToCustomEngineExporter/Urho3D/ConvertSpecularToMetallicRoughness"));
            tmpMaterial.SetTexture("_SpecGlossMap", texture);
            tmpMaterial.SetTexture("_MainTex", reference.Diffuse);
            tmpMaterial.SetFloat("_SmoothnessScale", reference.SmoothnessScale);
            tmpMaterial.SetTexture("_Smoothness", reference.Smoothness.Texture);
            try
            {
                new TextureProcessor().ProcessAndSaveTexture(reference.Diffuse, tmpMaterial,
                    _engine.GetTargetFilePath(baseColorName));
            }
            finally
            {
                Object.DestroyImmediate(tmpMaterial);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color Get(Color32[] texture, int texWidth, int texHeight, int x, int y, int width, int height)
        {
            var xx = x * texWidth / width;
            var yy = y * texHeight / height;
            return texture[xx + yy * texWidth];
        }
    }
}