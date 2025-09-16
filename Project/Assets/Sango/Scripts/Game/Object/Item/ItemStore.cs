using Newtonsoft.Json;
using System.Collections.Generic;

namespace Sango.Game
{
    public class ItemStore : IAarryDataObject
    {
        public List<ItemData> Items = new List<ItemData>();

        public IAarryDataObject FromArray(int[] values)
        {
            Items.Clear();
            if (values == null || values.Length == 0) return this;
            for (int i = 0; i < values.Length; i += 2)
            {
                int itemTypeId = values[i];
                int number = values[i + 1];

                ItemType itemType = Scenario.Cur.CommonData.ItemTypes.Get(itemTypeId);
                if (itemType == null) continue;
                Items.Add(new ItemData()
                {
                    itemType = itemType,
                    number = number,
                });
            }
            return this;
        }

        public int[] ToArray()
        {
            List<int> ints = new List<int>();
            foreach(ItemData item in Items)
            {
                ints.Add(item.itemType.Id); 
                ints.Add(item.number);
            }
            return ints.ToArray();
        }

        public ItemStore Copy()
        {
            ItemStore copy = new ItemStore();
            copy.Items = new List<ItemData>(Items);
            return copy;
        }
    }
}
