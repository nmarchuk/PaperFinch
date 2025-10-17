using System.ComponentModel;
using System.Linq;

namespace PaperFinch.Models
{
    public enum TrimSize
    {
        [Description("5×8 - Standard Trim Size")]
        Standard_5x8,

        [Description("5.25×8 - Standard Trim Size")]
        Standard_5_25x8,

        [Description("5.5×8.5 - Standard Trim Size")]
        Standard_5_5x8_5,

        [Description("6×9 - Standard Trim Size")]
        Standard_6x9,

        [Description("5.06×7.81 - Nonstandard Trim Size")]
        Nonstandard_5_06x7_81,

        [Description("5.5×8.25 - Nonstandard Trim Size")]
        Nonstandard_5_5x8_25,

        [Description("6.14×9.21 - Nonstandard Trim Size")]
        Nonstandard_6_14x9_21,

        [Description("4.72×7.48 - International Trim Size")]
        International_4_72x7_48,

        [Description("4.92×7.48 - International Trim Size")]
        International_4_92x7_48,

        [Description("5.83×8.27 - International Trim Size")]
        International_5_83x8_27,

        [Description("5.31×8.46 - International Trim Size")]
        International_5_31x8_46,

        [Description("4.12×6.75 - Mass Market Trim Size")]
        MassMarket_4_12x6_75,

        [Description("4.25×7 - Mass Market Trim Size")]
        MassMarket_4_25x7,

        [Description("4.37×7 - Mass Market Trim Size")]
        MassMarket_4_37x7
    }

    public static class TrimSizeExtensions
    {
        public static (float width, float height) GetDimensions(this TrimSize trimSize)
        {
            return trimSize switch
            {
                TrimSize.Standard_5x8 => (5f, 8f),
                TrimSize.Standard_5_25x8 => (5.25f, 8f),
                TrimSize.Standard_5_5x8_5 => (5.5f, 8.5f),
                TrimSize.Standard_6x9 => (6f, 9f),
                TrimSize.Nonstandard_5_06x7_81 => (5.06f, 7.81f),
                TrimSize.Nonstandard_5_5x8_25 => (5.5f, 8.25f),
                TrimSize.Nonstandard_6_14x9_21 => (6.14f, 9.21f),
                TrimSize.International_4_72x7_48 => (4.72f, 7.48f),
                TrimSize.International_4_92x7_48 => (4.92f, 7.48f),
                TrimSize.International_5_83x8_27 => (5.83f, 8.27f),
                TrimSize.International_5_31x8_46 => (5.31f, 8.46f),
                TrimSize.MassMarket_4_12x6_75 => (4.12f, 6.75f),
                TrimSize.MassMarket_4_25x7 => (4.25f, 7f),
                TrimSize.MassMarket_4_37x7 => (4.37f, 7f),
                _ => (6f, 9f)
            };
        }

        public static string GetDescription(this TrimSize trimSize)
        {
            var field = trimSize.GetType().GetField(trimSize.ToString());
            var attribute = (DescriptionAttribute?)field?.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
            return attribute?.Description ?? trimSize.ToString();
        }
    }
}