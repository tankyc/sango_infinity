using Newtonsoft.Json;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptOut)]
    public class TerrainType : SangoObject
    {
        public int foodDeposit;
        public float[] fertility = new float[2];
        public int goldDeposit;
        public float[] prosperity = new float[2];

        public float[] foodRate = new float[4];
        public ushort foodRegainDays;
        public float fireDamageRate;
        public bool viewThrough;
        public bool canBuild;
        public bool moveable;
        public bool canAmbush;
        public int baseCost;

        [JsonConverter(typeof(Color32Converter))]
        public Color32 color;


        public float Fertility
        {
            get
            {

                return GameRandom.Range(fertility[0], fertility[1]);
            }
        }

        public float Prosperity
        {
            get
            {
                return GameRandom.Range(prosperity[0], prosperity[1]);
            }
        }

        public int GetFood(int season, bool isRandom)
        {
            if (isRandom)
                return GetRandomFood(season);
            else
                return (int)(foodDeposit * foodRate[season]);
        }

        public int GetRandomFood(int season)
        {
            int num = GameUtility.Random(foodDeposit / 2) + ((foodDeposit * 3) / 4);
            return (int)(num * foodRate[season]);
        }
        public bool CanMoveBy(Troop troops)
        {
            return moveable;
        }

        //public override void Load(BinaryReader reader)
        //{
        //    base.Load(reader);
        //    Name = reader.ReadString();
        //    foodDeposit = reader.ReadInt32();
        //    for (int i = 0; i < 4; i++)
        //    {
        //        foodRate[i] = reader.ReadSingle();
        //    }
        //    foodRegainDays = reader.ReadUInt16();
        //    fireDamageRate = reader.ReadSingle();
        //    canBuild = reader.ReadBoolean();
        //    viewThrough = reader.ReadBoolean();
        //    moveable = reader.ReadBoolean();
        //}

        //public override void Save(BinaryWriter writer)
        //{
        //    base.Save(writer);
        //    writer.Write(Name);
        //    writer.Write(foodDeposit);
        //    for (int i = 0; i < 4; i++)
        //    {
        //        writer.Write(foodRate[i]);
        //    }
        //    writer.Write(foodRegainDays);
        //    writer.Write(fireDamageRate);
        //    writer.Write(canBuild);
        //    writer.Write(viewThrough);
        //    writer.Write(moveable);
        //}
    }
}
