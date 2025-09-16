
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

    [MenuItem("Sango/ͷ��༭���ߵ���ͷ������������")]
    public static void RenameHeadIconName()
    {
        string savedir = EditorUtility.OpenFolderPanel("ѡ��ͷ���ļ���", Application.dataPath, "");
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

    [MenuItem("Sango/������ͼ�ļ����ļ������滻")]
    public static void RenameTerrainTexName()
    {
        Sango.Path.Init();
        string savedir = EditorUtility.OpenFolderPanel("ѡ����ͼ�ļ���", Sango.Path.ContentRootPath, "");
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


    [MenuItem("Sango/ģ��WKMD->WKM")]
    public static void RenameWKMD2WKM()
    {
        string savedir = EditorUtility.OpenFolderPanel("ѡ���ļ���", Application.dataPath, "");
        string[] files = Sango.Directory.GetFiles(savedir, "*.WKMD", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string newFileName = f.Remove(f.Length - 4) + "wkm";
            Debug.Log(newFileName);
            Sango.File.Move(f, newFileName);
        }

    }

    [MenuItem("Sango/ѡ��ͼ����Сͼ")]
    static void ProcessToSprite()
    {
        Texture2D image = Selection.activeObject as Texture2D;//��ȡ��ת�Ķ���
        string rootPath = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(image));//��ȡ·������
        string path = rootPath + "/" + image.name + ".png";//ͼƬ·������

        TextureImporter texImp = AssetImporter.GetAtPath(path) as TextureImporter;

        AssetDatabase.CreateFolder(rootPath, image.name);//�����ļ���

        foreach (SpriteMetaData metaData in texImp.spritesheet)//����Сͼ��
        {
            Texture2D myimage = new Texture2D((int)metaData.rect.width, (int)metaData.rect.height);

            for (int y = (int)metaData.rect.y; y < metaData.rect.y + metaData.rect.height; y++)//Y������
            {
                for (int x = (int)metaData.rect.x; x < metaData.rect.x + metaData.rect.width; x++)
                    myimage.SetPixel(x - (int)metaData.rect.x, y - (int)metaData.rect.y, image.GetPixel(x, y));
            }

            //ת������EncodeToPNG���ݸ�ʽ
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
        Texture2D image = Selection.activeObject as Texture2D;//��ȡ��ת�Ķ���
        string rootPath = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(image));//��ȡ·������
        string path = rootPath + "/" + image.name + ".png";//ͼƬ·������

        TextureImporter texImp = AssetImporter.GetAtPath(path) as TextureImporter;

        AssetDatabase.CreateFolder(rootPath, image.name);//�����ļ���

        StringBuilder sb = new StringBuilder();
        foreach (SpriteMetaData metaData in texImp.spritesheet)//����Сͼ��
        {
            sb.AppendLine(string.Format("{0};{1};{2};{3};{4}; {5};{6}; {7};{8};{9};{10}",
                metaData.name, metaData.rect.x, metaData.rect.y, metaData.rect.width, metaData.rect.height,
                metaData.pivot.x, metaData.pivot.y,
                metaData.border.x, metaData.border.y, metaData.border.z, metaData.border.w));
        }

        System.IO.File.WriteAllText(rootPath + "/" + image.name + "/" + image.name + ".tpsheet", sb.ToString());

    }

}