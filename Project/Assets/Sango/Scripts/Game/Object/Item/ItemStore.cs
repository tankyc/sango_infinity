using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using static UnityEditor.Progress;

namespace Sango.Game
{
    public class ItemStore : IAarryDataObject
    {
        public Dictionary<int, int> Items = new Dictionary<int, int>();
        public int TotalNumber { get; private set; }

        public IAarryDataObject FromArray(int[] values)
        {
            TotalNumber = 0;
            Items.Clear();
            if (values == null || values.Length == 0) return this;
            for (int i = 0; i < values.Length; i += 2)
            {
                int itemTypeId = values[i];
                int number = values[i + 1];
                TotalNumber += number;
                ItemType itemType = Scenario.Cur.CommonData.ItemTypes.Get(itemTypeId);
                if (itemType == null) continue;
                Items.Add(itemTypeId, number);
            }
            return this;
        }

        public int[] ToArray()
        {
            List<int> ints = new List<int>();
            foreach (int itemTypeId in Items.Keys)
            {
                int number = Items[itemTypeId];
                if (number > 0)
                {
                    ints.Add(itemTypeId);
                    ints.Add(number);
                }
            }
            return ints.ToArray();
        }

        public ItemStore Copy()
        {
            ItemStore copy = new ItemStore();
            foreach (int itemTypeId in Items.Keys)
            {
                int number = Items[itemTypeId];
                if (number > 0)
                    copy.Items.Add(itemTypeId, number);
            }
            copy.TotalNumber = TotalNumber;
            return copy;
        }

        public int Add(int itemTypeId, int number)
        {
            if (Items.TryGetValue(itemTypeId, out int has))
            {
                has += number;
                Items[itemTypeId] = has;
                TotalNumber += number;
                return has;
            }

            TotalNumber += number;
            Items.Add(itemTypeId, number);
            return number;
        }

        public int Remove(int itemTypeId)
        {
            if (Items.TryGetValue(itemTypeId, out int has))
            {
                Items.Remove(itemTypeId);
                TotalNumber -= has;
                return has;
            }
            return 0;
        }

        public int Remove(int itemTypeId, int number)
        {
            if (Items.TryGetValue(itemTypeId, out int has))
            {
                number = Math.Max(has, number);
                has -= number;
                Items[itemTypeId] = has;
                TotalNumber -= number;
                return has;
            }
            return 0;
        }

        public int GetNumber(int itemTypeId)
        {
            if (Items.TryGetValue(itemTypeId, out int has))
                return has;
            return 0;
        }

        public int this[int itemTypeId]
        {
            get { return GetNumber(itemTypeId); }
            set { Add(itemTypeId, value); }
        }


    }
}
