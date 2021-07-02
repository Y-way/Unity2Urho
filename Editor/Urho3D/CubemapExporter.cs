﻿using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace UnityToCustomEngineExporter.Editor.Urho3D
{
    public class CubemapExporter
    {
        private readonly Urho3DEngine _engine;

        public CubemapExporter(Urho3DEngine engine)
        {
            _engine = engine;
        }

        public static bool EnsureReadableTexture(Cubemap texture)
        {
            if (null == texture) return false;

            var assetPath = AssetDatabase.GetAssetPath(texture);
            var tImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (tImporter != null)
            {
                //tImporter.textureType = TextureImporterType.Default;
                if (tImporter.isReadable != true)
                {
                    tImporter.isReadable = true;
                    AssetDatabase.ImportAsset(assetPath);
                    AssetDatabase.Refresh();
                }

                return true;
            }

            return false;
        }

        public void Cubemap(Cubemap texture)
        {
            var resourceName = EvaluateCubemapName(texture);
            var assetGuid = texture.GetKey();
            var sourceFileTimestampUtc = ExportUtils.GetLastWriteTimeUtc(texture);
            if (_engine.IsUpToDate(assetGuid, resourceName, sourceFileTimestampUtc)) return;

            if (!EnsureReadableTexture(texture))
                return;

            using (var writer =
                _engine.TryCreateXml(assetGuid, resourceName, sourceFileTimestampUtc))
            {
                if (writer != null) WriteCubemap(texture, resourceName, writer);
            }
        }

        public string EvaluateCubemapName(Texture cubemap)
        {
            return ExportUtils.ReplaceExtension(ExportUtils.GetRelPathFromAsset(_engine.Options.Subfolder, cubemap),
                ".xml");
        }

        private void WriteCubemap(Cubemap texture, string resourceName, XmlWriter writer)
        {
            var ddsName = resourceName.Replace(".xml", ".dds");

            var assetPath = AssetDatabase.GetAssetPath(texture);
            var tImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            var srgb = tImporter?.sRGBTexture ?? false;
            DDS.SaveAsRgbaDds(texture, _engine.GetTargetFilePath(ddsName), false);
            writer.WriteStartElement("cubemap");
            writer.WriteWhitespace(Environment.NewLine);
            writer.WriteStartElement("srgb");
            writer.WriteAttributeString("enable", srgb ? "true" : "false");
            writer.WriteEndElement();
            writer.WriteStartElement("image");
            writer.WriteAttributeString("name", Path.GetFileName(ddsName));
            writer.WriteEndElement();
            writer.WriteWhitespace(Environment.NewLine);
            writer.WriteEndElement();
        }
    }
}