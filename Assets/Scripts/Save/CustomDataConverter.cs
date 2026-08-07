using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Generation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IdleDefenseSurvival.Save
{
    /// <summary>
    /// InventoryItem.CustomData holds typed arrays (CombatStatEntry[], AffixInstanceData[])
    /// in a Dictionary&lt;string, object&gt;. Without this, Newtonsoft round-trips those
    /// values as JArray/JObject (declared type is object) and the `is CombatStatEntry[]`
    /// casts in EquipmentStatCalculator/EquipmentComparer/InventoryInfoPanel fail after load.
    /// Write uses default runtime-type serialization; read rebuilds the known types.
    /// </summary>
    public class CustomDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Dictionary<string, object>);

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var dict = new Dictionary<string, object>();
            if (JToken.ReadFrom(reader) is not JObject obj) return dict;

            foreach (var prop in obj.Properties())
            {
                switch (prop.Name)
                {
                    case "SecondaryStats":
                        dict[prop.Name] = prop.Value.ToObject<CombatStatEntry[]>(serializer);
                        break;
                    case "Affixes":
                        dict[prop.Name] = prop.Value.ToObject<AffixInstanceData[]>(serializer);
                        break;
                    default:
                        dict[prop.Name] = prop.Value;
                        break;
                }
            }
            return dict;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

    }
}
