
using UnityEditor;
using UnityEngine;
using System.IO;
using Sango;
using System.Collections.Generic;
using System.Text;
using Sango.Game;
using Codice.Client.Common;
using Unity.VisualScripting;
using System;
using System.Linq;
using Newtonsoft.Json;
using System.Xml;
using Sango.Data;
using System.Reflection;
using System.Collections;

public static class SangeEditorTools
{

    [MenuItem("Sango/头像编辑工具导出头像名字批处理")]
    public static void RenameHeadIconName()
    {
        string savedir = EditorUtility.OpenFolderPanel("选择头像文件夹", Application.dataPath, "");
        string[] files = Sango.Directory.GetFiles(savedir, "*.png", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(f);
            string[] s = fileName.Split("_");
            int id;
            int part;
            int.TryParse(s[0], out id);
            int.TryParse(s[2], out part);

            Sango.File.Move(f, string.Format("{0}/{1}_{2}.png", savedir, id, part));

        }

    }

    [MenuItem("Sango/地形贴图文件夹文件名字替换")]
    public static void RenameTerrainTexName()
    {
        Sango.Path.Init();
        string savedir = EditorUtility.OpenFolderPanel("选择贴图文件夹", Sango.Path.ContentRootPath, "");
        string[] files = Sango.Directory.GetFiles(savedir, "*.png", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(f);
            string[] s = fileName.Split("_");
            if(s.Length > 1)
            {
                int id;
                int.TryParse(s[1], out id);
                Sango.File.Move(f, string.Format("{0}/layer_{1}.png", savedir, id));
            }
        }

    }


    [MenuItem("Sango/模型WKMD->WKM")]
    public static void RenameWKMD2WKM()
    {
        string savedir = EditorUtility.OpenFolderPanel("选择文件夹", Application.dataPath, "");
        string[] files = Sango.Directory.GetFiles(savedir, "*.WKMD", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string newFileName = f.Remove(f.Length - 4) + "wkm";
            Debug.Log(newFileName);
            Sango.File.Move(f, newFileName);
        }

    }

    [MenuItem("Sango/选中图集拆小图")]
    static void ProcessToSprite()
    {
        Texture2D image = Selection.activeObject as Texture2D;//获取旋转的对象
        string rootPath = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(image));//获取路径名称
        string path = rootPath + "/" + image.name + ".png";//图片路径名称

        TextureImporter texImp = AssetImporter.GetAtPath(path) as TextureImporter;

        AssetDatabase.CreateFolder(rootPath, image.name);//创建文件夹

        foreach (SpriteMetaData metaData in texImp.spritesheet)//遍历小图集
        {
            Texture2D myimage = new Texture2D((int)metaData.rect.width, (int)metaData.rect.height);

            for (int y = (int)metaData.rect.y; y < metaData.rect.y + metaData.rect.height; y++)//Y轴像素
            {
                for (int x = (int)metaData.rect.x; x < metaData.rect.x + metaData.rect.width; x++)
                    myimage.SetPixel(x - (int)metaData.rect.x, y - (int)metaData.rect.y, image.GetPixel(x, y));
            }

            //转换纹理到EncodeToPNG兼容格式
            if (myimage.format != TextureFormat.ARGB32 && myimage.format != TextureFormat.RGB24)
            {
                Texture2D newTexture = new Texture2D(myimage.width, myimage.height);
                newTexture.SetPixels(myimage.GetPixels(0), 0);
                myimage = newTexture;
            }
            var pngData = myimage.EncodeToPNG();

            System.IO.File.WriteAllBytes(rootPath + "/" + image.name + "/" + metaData.name + ".png", pngData);
        }
    }


    [MenuItem("Assets/Sprite Sheet Packer/Process to Sprites Info")]
    static void ProcessToSpriteInfo()
    {
        Texture2D image = Selection.activeObject as Texture2D;//获取旋转的对象
        string rootPath = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(image));//获取路径名称
        string path = rootPath + "/" + image.name + ".png";//图片路径名称

        TextureImporter texImp = AssetImporter.GetAtPath(path) as TextureImporter;

        AssetDatabase.CreateFolder(rootPath, image.name);//创建文件夹

        StringBuilder sb = new StringBuilder();
        foreach (SpriteMetaData metaData in texImp.spritesheet)//遍历小图集
        {
            sb.AppendLine(string.Format("{0};{1};{2};{3};{4}; {5};{6}; {7};{8};{9};{10}",
                metaData.name, metaData.rect.x, metaData.rect.y, metaData.rect.width, metaData.rect.height,
                metaData.pivot.x, metaData.pivot.y,
                metaData.border.x, metaData.border.y, metaData.border.z, metaData.border.w));
        }

        System.IO.File.WriteAllText(rootPath + "/" + image.name + "/" + image.name + ".tpsheet", sb.ToString());

    }

}