namespace ChroniclesOfRebirth.Core.Pixel
{
    /// <summary>
    /// Палитра осени по визуальному эталону (Docs/ArtDirection/References).
    /// Каждый набор — от тёмного к светлому. Смена сезона — это другая палитра с теми же генераторами.
    /// </summary>
    public static class AutumnPalette
    {
        // Палитры земли — 8–10 оттенков со смещением тона: тени уходят в холодный (сине-зелёный, сиреневый),
        // света — в тёплый жёлтый. Так переходы мягче, а цвет «живой», как в эталоне.
        // Трава природная: глубокие зелёные тени, оливковые средние тона, тёплые, но не неоновые света.
        public static readonly ColorRamp Grass = new("121e18", "19291d", "213522", "2a4226", "344f2a", "405c2e", "4e6a33", "5e7839", "708641", "85944c");
        // Сухая трава — солома и пожухлые стебли, ближе к бежевому, чем к жёлтому.
        public static readonly ColorRamp DryGrass = new("332c1e", "463c25", "5b4e2d", "716035", "86723f", "9a854c", "ad975c", "bea96f", "cdba85");
        public static readonly ColorRamp ForestFloor = new("15201a", "1c2a1d", "253621", "2f4325", "3b5128", "4a5f2d", "5a6c33", "6c7a3b", "7f8946");
        public static readonly ColorRamp Dirt = new("2a1c14", "3a2617", "4d3219", "613f1f", "774f27", "8d6031", "a3733d", "b8874d", "ca9b61", "dab07a");
        public static readonly ColorRamp Pebble = new("4a4640", "5b564f", "6d675e", "807970", "948c81", "a89f92", "bcb3a5", "cfc6b8");
        public static readonly ColorRamp PebbleGap = new("3e362c", "4f4538", "5f5446");
        public static readonly ColorRamp Sand = new("4f4436", "5f5241", "706150", "827160", "958370", "a79580", "b9a791", "cab9a3");
        public static readonly ColorRamp Riverbed = new("33463f", "3d5048", "4e655a", "62796b", "778f7e", "8ea594", "a3b8a6");
        public static readonly ColorRamp RiverbedGap = new("2c3a34", "34443d");
        public static readonly ColorRamp Stone = new("2c2f35", "393d42", "484c50", "595d5f", "6b6e6d", "7f807b", "94938b", "a9a79c", "c0bdb0");
        public static readonly ColorRamp Mortar = new("23221f", "2f2d29");
        public static readonly ColorRamp Moss = new("1c3019", "25401f", "305226", "3d652c", "4c7832", "5e8b39", "729e42");
        public static readonly ColorRamp Earth = new("201712", "2e2017", "3d2b1d", "4c3623", "5c432a", "6c5133", "7d6040", "8f7050");
        public static readonly ColorRamp Bark = new("1c130e", "271a12", "332318", "412d1f", "503926", "61462f", "725439", "846444");
        public static readonly ColorRamp Rock = new("2c2b2e", "3a3836", "4b4842", "5e5950", "726c61", "878072", "9d9585", "b3ab9a", "c9c0ae");
        /// <summary>Горные обрывы: тёплый серо-коричневый камень со слоями, тени — в сиреневый.</summary>
        public static readonly ColorRamp CliffRock = new("231f20", "312b29", "413832", "52463d", "645549", "776556", "8b7765", "a08a77", "b59d89");
        /// <summary>
        /// Снег в утреннем свете: сиреневые тени, кремовые света — тёплый, а не холодно-белый. Альбедо заметно ниже
        /// белого: под солнцем интенсивности 3 светлый снег пересвечивался в сплошные белые пятна.
        /// </summary>
        public static readonly ColorRamp Snow = new("5c6079", "71748e", "8a8aa2", "a19fb2", "b6b0ba", "c7beba", "d5c9bf");

        public static readonly ColorRamp LeavesOrange = new("4a1a0f", "752812", "a33c17", "cc5a22", "e8822f", "f5a947", "fbcf72");
        public static readonly ColorRamp LeavesRed = new("3d1110", "631a16", "8c261d", "b23827", "d45334", "ea7a48");
        public static readonly ColorRamp LeavesYellow = new("5a3d10", "855e17", "ae8320", "d1a62e", "e9c64a", "f6e07a");
        public static readonly ColorRamp LeavesConifer = new("0f1f17", "162c20", "1f3b29", "2a4c33", "37603e", "467449");
        /// <summary>Опавшие листья на земле: тёмно-красные, охра, оранжевые и жёлтые вперемешку.</summary>
        public static readonly ColorRamp FallenLeaves = new("4d1712", "6e2416", "8c3a1a", "a8551f", "c26e27", "d48a2c", "dba63a", "e2c056");
        public static readonly ColorRamp LeavesBush = new("1c2612", "2b3a19", "3e5222", "556a2b", "718536", "929f46");

        public static readonly ColorRamp Wood = new("3a2616", "543820", "6e4b2c", "8a6038", "a5774a", "bd8f5c");
        public static readonly ColorRamp Canvas = new("6f6249", "8a7a5e", "a39275", "b9a88a", "cdbd9e");
        public static readonly ColorRamp RibbonRed = new("7a1f1a", "a8322a", "cf5a43");
        public static readonly ColorRamp RibbonBlue = new("233f6b", "34609a", "5a8cc4");
        public static readonly ColorRamp Mushroom = new("1f3d4a", "2f6a7a", "4fa9b8", "8fe3e8", "d8fbff");

        public static readonly ColorRamp FlowerWhite = new("b9bcb4", "dcded6", "f4f4ee", "ffffff");
        public static readonly ColorRamp FlowerCenter = new("c9962b", "f0cc4e");

        public static readonly ColorRamp Scorch = new("120d0a", "1f1611", "2e2219", "3d2f22");
        public static readonly ColorRamp PortalStone = new("16181d", "1f2228", "2a2e35", "363b43", "434952");
        public static readonly ColorRamp PortalGlow = new("3e8fc4", "6cc3ef", "a6e6ff", "e2f8ff");
    }
}
