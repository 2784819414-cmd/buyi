using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    internal static class StorageGeneratedIconFactory
    {
        private const string GeneratedIconVersion = "generated_v2|";
        private const int PixelsPerCell = 64;
        private const int MinPixels = 64;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private enum IconShape
        {
            Generic,
            Phone,
            Key,
            Note,
            Book,
            Workbook,
            PencilCase,
            LunchBox,
            PacketWide,
            PacketTall,
            Can,
            Bottle,
            Carton,
            Cup,
            Onigiri,
            Bread,
            CandyBar,
            TissuePack,
            StickyNotes,
            Pen,
            Eraser,
            Ruler,
            CorrectionTape,
            Batteries,
            BandageBox,
            Toothbrush,
            Toothpaste,
            Soap,
            Umbrella,
            Sanitizer,
            HairTiePack,
            Backpack
        }

        public static bool TryCreate(string definitionId, int width, int height, out Sprite sprite)
        {
            string normalizedId = StorageItemIconUtility.NormalizeDefinitionId(definitionId);
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                sprite = null;
                return false;
            }

            int clampedWidth = Mathf.Max(1, width);
            int clampedHeight = Mathf.Max(1, height);
            string cacheKey = GeneratedIconVersion + normalizedId + "|" + clampedWidth + "x" + clampedHeight;
            if (Cache.TryGetValue(cacheKey, out sprite))
            {
                return sprite != null;
            }

            IconRecipe recipe = ResolveRecipe(normalizedId);
            sprite = BuildSprite(cacheKey, clampedWidth, clampedHeight, recipe);
            Cache[cacheKey] = sprite;
            return sprite != null;
        }

        private static Sprite BuildSprite(string cacheKey, int widthCells, int heightCells, IconRecipe recipe)
        {
            int textureWidth = Mathf.Max(MinPixels, widthCells * PixelsPerCell);
            int textureHeight = Mathf.Max(MinPixels, heightCells * PixelsPerCell);
            Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            texture.name = cacheKey;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[textureWidth * textureHeight];
            Clear(pixels);

            RectInt canvas = CreateCanvas(textureWidth, textureHeight);
            DrawShape(pixels, textureWidth, textureHeight, canvas, recipe);

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureWidth, textureHeight),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = cacheKey;
            return sprite;
        }

        private static RectInt CreateCanvas(int textureWidth, int textureHeight)
        {
            int paddingX = Mathf.Max(4, Mathf.RoundToInt(textureWidth * 0.08f));
            int paddingY = Mathf.Max(4, Mathf.RoundToInt(textureHeight * 0.08f));
            return new RectInt(
                paddingX,
                paddingY,
                Mathf.Max(1, textureWidth - paddingX * 2),
                Mathf.Max(1, textureHeight - paddingY * 2));
        }

        private static void DrawShape(Color32[] pixels, int textureWidth, int textureHeight, RectInt canvas, IconRecipe recipe)
        {
            switch (recipe.Shape)
            {
                case IconShape.Phone:
                    DrawPhone(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Key:
                    DrawKey(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Note:
                    DrawNote(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Book:
                    DrawBook(pixels, textureWidth, textureHeight, canvas, recipe, false);
                    return;
                case IconShape.Workbook:
                    DrawBook(pixels, textureWidth, textureHeight, canvas, recipe, true);
                    return;
                case IconShape.PencilCase:
                    DrawPencilCase(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.LunchBox:
                    DrawLunchBox(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.PacketWide:
                    DrawPacket(pixels, textureWidth, textureHeight, canvas, recipe, false);
                    return;
                case IconShape.PacketTall:
                    DrawPacket(pixels, textureWidth, textureHeight, canvas, recipe, true);
                    return;
                case IconShape.Can:
                    DrawCan(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Bottle:
                    DrawBottle(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Carton:
                    DrawCarton(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Cup:
                    DrawCup(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Onigiri:
                    DrawOnigiri(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Bread:
                    DrawBread(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.CandyBar:
                    DrawCandyBar(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.TissuePack:
                    DrawTissuePack(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.StickyNotes:
                    DrawStickyNotes(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Pen:
                    DrawPen(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Eraser:
                    DrawEraser(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Ruler:
                    DrawRuler(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.CorrectionTape:
                    DrawCorrectionTape(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Batteries:
                    DrawBatteries(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.BandageBox:
                    DrawBandageBox(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Toothbrush:
                    DrawToothbrush(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Toothpaste:
                    DrawToothpaste(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Soap:
                    DrawSoap(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Umbrella:
                    DrawUmbrella(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Sanitizer:
                    DrawSanitizer(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.HairTiePack:
                    DrawHairTiePack(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                case IconShape.Backpack:
                    DrawBackpack(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
                default:
                    DrawGeneric(pixels, textureWidth, textureHeight, canvas, recipe);
                    return;
            }
        }

        private static IconRecipe ResolveRecipe(string normalizedId)
        {
            switch (normalizedId)
            {
                case "phone":
                    return Recipe(IconShape.Phone, "#3A7FA6", "#B7D8E7", "#0D3046");
                case "key":
                    return Recipe(IconShape.Key, "#D6B25A", "#F7E1A0", "#6B5222");
                case "note":
                    return Recipe(IconShape.Note, "#ECE2BF", "#FFF7D9", "#6A6047");
                case "textbook":
                    return Recipe(IconShape.Book, "#456E9A", "#D2E3F2", "#1D3553");
                case "workbook":
                    return Recipe(IconShape.Workbook, "#6AA38C", "#D2F0E4", "#224F42");
                case "pencil_case":
                    return Recipe(IconShape.PencilCase, "#6E5E91", "#DFD7F6", "#302747");
                case "lunch_box":
                    return Recipe(IconShape.LunchBox, "#C48546", "#F6D4A8", "#5A381C");
                case "snack":
                    return Recipe(IconShape.PacketWide, "#C86A43", "#F3C497", "#5A2516");
                case "school_backpack":
                    return Recipe(IconShape.Backpack, "#3C5E78", "#E1B85C", "#172B3C");
            }

            if (normalizedId.Contains("pen") || normalizedId.Contains("pencil") || normalizedId.Contains("highlighter"))
            {
                return Recipe(IconShape.Pen, "#4C91C7", "#E2F3FF", "#173852");
            }

            if (normalizedId.Contains("eraser"))
            {
                return Recipe(IconShape.Eraser, "#E78BA2", "#FFE1EA", "#6A3140");
            }

            if (normalizedId.Contains("ruler"))
            {
                return Recipe(IconShape.Ruler, "#D7B565", "#FBECC0", "#6A5421");
            }

            if (normalizedId.Contains("correction_tape"))
            {
                return Recipe(IconShape.CorrectionTape, "#8AA3B8", "#F0F7FF", "#36495B");
            }

            if (normalizedId.Contains("sticky_notes"))
            {
                return Recipe(IconShape.StickyNotes, "#F2D45C", "#FFF6B0", "#6B5A1B");
            }

            if (normalizedId.Contains("notebook"))
            {
                return Recipe(IconShape.Workbook, "#5A8CB2", "#DDEFFA", "#213B53");
            }

            if (normalizedId.Contains("toothbrush"))
            {
                return Recipe(IconShape.Toothbrush, "#69B6C8", "#E1FCFF", "#1E4952");
            }

            if (normalizedId.Contains("toothpaste"))
            {
                return Recipe(IconShape.Toothpaste, "#6BA0D7", "#E5F2FF", "#234669");
            }

            if (normalizedId.Contains("soap"))
            {
                return Recipe(IconShape.Soap, "#8FC4D1", "#ECFDFF", "#2D5961");
            }

            if (normalizedId.Contains("umbrella"))
            {
                return Recipe(IconShape.Umbrella, "#5874AC", "#DFE8FF", "#21304E");
            }

            if (normalizedId.Contains("sanitizer"))
            {
                return Recipe(IconShape.Sanitizer, "#7FC7D9", "#E8FCFF", "#28505A");
            }

            if (normalizedId.Contains("hair_tie"))
            {
                return Recipe(IconShape.HairTiePack, "#B582B8", "#F5DEF6", "#4C2A4E");
            }

            if (normalizedId.Contains("bandage"))
            {
                return Recipe(IconShape.BandageBox, "#D5B47A", "#FFF0C7", "#694A24");
            }

            if (normalizedId.Contains("battery"))
            {
                return Recipe(IconShape.Batteries, "#D69B40", "#FFF0C0", "#59380F");
            }

            if (normalizedId.Contains("tissue") || normalizedId.Contains("wet_wipes"))
            {
                return Recipe(IconShape.TissuePack, "#8AB7C9", "#ECF8FF", "#30505D");
            }

            if (normalizedId.Contains("onigiri") || normalizedId.Contains("rice_ball"))
            {
                return Recipe(IconShape.Onigiri, "#F2F2EA", "#FFFFFF", "#3A4B58");
            }

            if (normalizedId.Contains("bread") || normalizedId.Contains("sandwich") || normalizedId.Contains("sausage_roll"))
            {
                return Recipe(IconShape.Bread, "#D09A52", "#FFF0CC", "#6C431C");
            }

            if (normalizedId.Contains("cookies") || normalizedId.Contains("chips") || normalizedId.Contains("crackers") ||
                normalizedId.Contains("peanuts") || normalizedId.Contains("seaweed") || normalizedId.Contains("popcorn"))
            {
                return Recipe(IconShape.PacketWide, "#C8864F", "#FCE0B5", "#5A341B");
            }

            if (normalizedId.Contains("mint_gum") || normalizedId.Contains("chocolate_bar"))
            {
                return Recipe(IconShape.CandyBar, "#7A9D6B", "#E5F4D8", "#314528");
            }

            if (normalizedId.Contains("coffee_can") || normalizedId.Contains("energy_drink") || normalizedId.Contains("soda"))
            {
                return Recipe(IconShape.Can, "#5C8AC1", "#E0F1FF", "#213B5A");
            }

            if (normalizedId.Contains("water") || normalizedId.Contains("green_tea") || normalizedId.Contains("yogurt_drink"))
            {
                return Recipe(IconShape.Bottle, "#6AA7D8", "#E5F6FF", "#224A67");
            }

            if (normalizedId.Contains("juice_box") || normalizedId.Contains("milk_box"))
            {
                return Recipe(IconShape.Carton, "#8EC9A1", "#ECFFEF", "#2B5A35");
            }

            if (normalizedId.Contains("cup") || normalizedId.Contains("pudding") || normalizedId.Contains("miso_soup"))
            {
                return Recipe(IconShape.Cup, "#B98F63", "#FFF0D9", "#52351A");
            }

            if (normalizedId.Contains("instant"))
            {
                return Recipe(IconShape.Cup, "#C89152", "#FFF2D3", "#653E18");
            }

            return Recipe(IconShape.Generic, "#7B8A95", "#EEF5FA", "#2B3842");
        }

        private static IconRecipe Recipe(IconShape shape, string fill, string accent, string stroke)
        {
            return new IconRecipe(shape, ParseColor(fill), ParseColor(accent), ParseColor(stroke));
        }

        private static void DrawGeneric(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            FillRoundedRect(pixels, width, height, canvas, recipe.Fill, Mathf.RoundToInt(canvas.width * 0.14f));
            RectInt plate = Inset(canvas, canvas.width / 6, canvas.height / 6);
            FillRoundedRect(pixels, width, height, plate, recipe.Accent, Mathf.RoundToInt(plate.width * 0.12f));
            StrokeRoundedRect(pixels, width, height, canvas, recipe.Stroke, Mathf.Max(2, canvas.width / 18), Mathf.RoundToInt(canvas.width * 0.14f));
        }

        private static void DrawPhone(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 4, canvas.height / 10);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.width * 0.22f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.width / 12), Mathf.RoundToInt(body.width * 0.22f));
            RectInt screen = Inset(body, body.width / 7, body.height / 8);
            FillRoundedRect(pixels, width, height, screen, recipe.Accent, Mathf.RoundToInt(screen.width * 0.12f));
            DrawLine(pixels, width, height, screen.xMin + screen.width / 3, screen.yMin + screen.height / 5, screen.xMin + screen.width * 2 / 3, screen.yMin + screen.height * 4 / 5, recipe.Stroke, Mathf.Max(2, screen.width / 10));
            DrawCircle(pixels, width, height, CenterX(body), body.yMin + body.height / 12, Mathf.Max(1, body.width / 18), recipe.Accent);
        }

        private static void DrawKey(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            int radius = Mathf.Max(6, Mathf.Min(canvas.width, canvas.height) / 5);
            Vector2Int head = new Vector2Int(canvas.xMin + canvas.width / 3, CenterY(canvas));
            DrawRing(pixels, width, height, head, radius, Mathf.Max(4, radius / 3), recipe.Fill, recipe.Stroke);
            RectInt shaft = new RectInt(head.x + radius / 2, head.y - radius / 6, canvas.width / 2, Mathf.Max(6, radius / 3));
            FillRect(pixels, width, height, shaft, recipe.Fill);
            StrokeRect(pixels, width, height, shaft, recipe.Stroke, Mathf.Max(2, shaft.height / 4));
            RectInt toothA = new RectInt(shaft.xMax - shaft.width / 4, shaft.yMax - 2, Mathf.Max(6, shaft.width / 8), radius / 2);
            RectInt toothB = new RectInt(shaft.xMax - shaft.width / 9, shaft.yMax - 2, Mathf.Max(6, shaft.width / 8), radius / 3);
            FillRect(pixels, width, height, toothA, recipe.Fill);
            FillRect(pixels, width, height, toothB, recipe.Fill);
        }

        private static void DrawNote(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt sheet = Inset(canvas, canvas.width / 8, canvas.height / 10);
            FillRoundedRect(pixels, width, height, sheet, recipe.Fill, Mathf.RoundToInt(sheet.width * 0.08f));
            StrokeRoundedRect(pixels, width, height, sheet, recipe.Stroke, Mathf.Max(2, sheet.width / 24), Mathf.RoundToInt(sheet.width * 0.08f));
            RectInt fold = new RectInt(sheet.xMax - sheet.width / 4, sheet.yMax - sheet.height / 4, sheet.width / 4, sheet.height / 4);
            FillTriangle(pixels, width, height,
                new Vector2Int(fold.xMin, fold.yMax),
                new Vector2Int(fold.xMax, fold.yMax),
                new Vector2Int(fold.xMax, fold.yMin),
                recipe.Accent);
            DrawRuleLines(pixels, width, height, sheet, recipe.Stroke, 3);
        }

        private static void DrawBook(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe, bool workbook)
        {
            RectInt cover = Inset(canvas, canvas.width / 10, canvas.height / 10);
            FillRoundedRect(pixels, width, height, cover, recipe.Fill, Mathf.RoundToInt(cover.width * 0.08f));
            StrokeRoundedRect(pixels, width, height, cover, recipe.Stroke, Mathf.Max(2, cover.width / 28), Mathf.RoundToInt(cover.width * 0.08f));
            RectInt spine = new RectInt(cover.xMin + cover.width / 10, cover.yMin, Mathf.Max(6, cover.width / 7), cover.height);
            FillRect(pixels, width, height, spine, recipe.Stroke);
            RectInt label = new RectInt(CenterX(cover) - cover.width / 5, cover.yMin + cover.height / 5, cover.width / 3, cover.height / 5);
            FillRoundedRect(pixels, width, height, label, recipe.Accent, Mathf.RoundToInt(label.height * 0.2f));
            if (workbook)
            {
                DrawRuleLines(pixels, width, height, Inset(cover, cover.width / 3, cover.height / 4), recipe.Accent, 3);
            }
        }

        private static void DrawPencilCase(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 10, canvas.height / 4);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.45f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.height / 10), Mathf.RoundToInt(body.height * 0.45f));
            int bodyCenterY = CenterY(body);
            DrawLine(pixels, width, height, body.xMin + body.width / 4, bodyCenterY, body.xMax - body.width / 5, bodyCenterY, recipe.Accent, Mathf.Max(2, body.height / 8));
            DrawCircle(pixels, width, height, body.xMin + body.width * 3 / 4, bodyCenterY, Mathf.Max(2, body.height / 10), recipe.Accent);
        }

        private static void DrawLunchBox(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 9, canvas.height / 6);
            RectInt lid = new RectInt(body.xMin + body.width / 8, body.yMax - body.height / 3, body.width * 3 / 4, body.height / 4);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.width * 0.12f));
            FillRoundedRect(pixels, width, height, lid, recipe.Accent, Mathf.RoundToInt(lid.height * 0.35f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.width / 20), Mathf.RoundToInt(body.width * 0.12f));
            int bodyCenterX = CenterX(body);
            DrawLine(pixels, width, height, bodyCenterX, body.yMin + body.height / 5, bodyCenterX, body.yMax - body.height / 5, recipe.Stroke, Mathf.Max(2, body.width / 20));
        }

        private static void DrawPacket(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe, bool tall)
        {
            RectInt body = tall
                ? Inset(canvas, canvas.width / 6, canvas.height / 12)
                : Inset(canvas, canvas.width / 14, canvas.height / 6);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(Mathf.Min(body.width, body.height) * 0.12f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, Mathf.Min(body.width, body.height) / 18), Mathf.RoundToInt(Mathf.Min(body.width, body.height) * 0.12f));
            RectInt band = tall
                ? new RectInt(body.xMin + body.width / 5, CenterY(body) - body.height / 8, body.width * 3 / 5, body.height / 4)
                : new RectInt(CenterX(body) - body.width / 10, body.yMin + body.height / 6, body.width / 5, body.height * 2 / 3);
            FillRoundedRect(pixels, width, height, band, recipe.Accent, Mathf.RoundToInt(Mathf.Min(band.width, band.height) * 0.18f));
        }

        private static void DrawCan(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 5, canvas.height / 10);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.width * 0.45f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.width / 12), Mathf.RoundToInt(body.width * 0.45f));
            RectInt label = new RectInt(body.xMin + body.width / 7, CenterY(body) - body.height / 5, body.width * 5 / 7, body.height * 2 / 5);
            FillRoundedRect(pixels, width, height, label, recipe.Accent, Mathf.RoundToInt(label.height * 0.25f));
        }

        private static void DrawBottle(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = new RectInt(CenterX(canvas) - canvas.width / 5, canvas.yMin + canvas.height / 7, canvas.width * 2 / 5, canvas.height * 11 / 18);
            RectInt shoulder = new RectInt(body.xMin + body.width / 8, body.yMax - body.height / 6, body.width * 3 / 4, body.height / 6);
            RectInt neck = new RectInt(CenterX(body) - body.width / 6, shoulder.yMax - 1, body.width / 3, canvas.height / 9);
            RectInt cap = new RectInt(CenterX(neck) - neck.width / 2, neck.yMax - 1, neck.width, canvas.height / 14);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.width * 0.4f));
            FillRoundedRect(pixels, width, height, shoulder, recipe.Fill, Mathf.RoundToInt(shoulder.height * 0.45f));
            FillRoundedRect(pixels, width, height, neck, recipe.Fill, Mathf.RoundToInt(neck.width * 0.25f));
            FillRect(pixels, width, height, cap, recipe.Stroke);
            RectInt label = new RectInt(body.xMin + body.width / 7, CenterY(body) - body.height / 7, body.width * 5 / 7, body.height / 3);
            FillRoundedRect(pixels, width, height, label, recipe.Accent, Mathf.RoundToInt(label.height * 0.3f));
            DrawLine(pixels, width, height, CenterX(body), body.yMin + body.height / 8, CenterX(body), body.yMax - body.height / 8, recipe.Accent, Mathf.Max(2, body.width / 10));
        }

        private static void DrawCarton(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 5, canvas.height / 8);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.width * 0.08f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.width / 18), Mathf.RoundToInt(body.width * 0.08f));
            FillTriangle(
                pixels,
                width,
                height,
                new Vector2Int(body.xMin, body.yMax - body.height / 5),
                new Vector2Int(CenterX(body), body.yMax),
                new Vector2Int(body.xMax, body.yMax - body.height / 5),
                recipe.Accent);
        }

        private static void DrawCup(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt bowl = Inset(canvas, canvas.width / 6, canvas.height / 6);
            FillRoundedRect(pixels, width, height, bowl, recipe.Fill, Mathf.RoundToInt(bowl.width * 0.25f));
            StrokeRoundedRect(pixels, width, height, bowl, recipe.Stroke, Mathf.Max(2, bowl.width / 18), Mathf.RoundToInt(bowl.width * 0.25f));
            RectInt rim = new RectInt(bowl.xMin, bowl.yMax - bowl.height / 6, bowl.width, bowl.height / 8);
            FillRoundedRect(pixels, width, height, rim, recipe.Accent, Mathf.RoundToInt(rim.height * 0.45f));
            RectInt handle = new RectInt(bowl.xMax - bowl.width / 10, CenterY(bowl) - bowl.height / 6, bowl.width / 5, bowl.height / 3);
            DrawRing(
                pixels,
                width,
                height,
                new Vector2Int(handle.xMin + handle.width / 2, handle.yMin + handle.height / 2),
                Mathf.Max(5, handle.height / 2),
                Mathf.Max(3, handle.width / 4),
                recipe.Accent,
                recipe.Stroke);
        }

        private static void DrawOnigiri(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            Vector2Int a = new Vector2Int(CenterX(canvas), canvas.yMax);
            Vector2Int b = new Vector2Int(canvas.xMin + canvas.width / 7, canvas.yMin + canvas.height / 5);
            Vector2Int c = new Vector2Int(canvas.xMax - canvas.width / 7, canvas.yMin + canvas.height / 5);
            FillTriangle(pixels, width, height, a, b, c, recipe.Fill);
            FillRect(
                pixels,
                width,
                height,
                new RectInt(CenterX(canvas) - canvas.width / 8, canvas.yMin + canvas.height / 5, canvas.width / 4, canvas.height / 3),
                recipe.Stroke);
        }

        private static void DrawBread(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 10, canvas.height / 5);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.5f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.height / 10), Mathf.RoundToInt(body.height * 0.5f));
            DrawArcScore(pixels, width, height, body, recipe.Accent);
        }

        private static void DrawCandyBar(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 14, canvas.height / 5);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.3f));
            RectInt band = new RectInt(CenterX(body) - body.width / 7, body.yMin, body.width / 3, body.height);
            FillRoundedRect(pixels, width, height, band, recipe.Accent, Mathf.RoundToInt(band.height * 0.18f));
        }

        private static void DrawTissuePack(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 12, canvas.height / 5);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.35f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.height / 10), Mathf.RoundToInt(body.height * 0.35f));
            RectInt slit = new RectInt(CenterX(body) - body.width / 6, CenterY(body) - body.height / 10, body.width / 3, body.height / 5);
            FillRoundedRect(pixels, width, height, slit, recipe.Accent, Mathf.RoundToInt(slit.height * 0.45f));
        }

        private static void DrawStickyNotes(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt back = Inset(canvas, canvas.width / 5, canvas.height / 5);
            RectInt front = new RectInt(back.xMin + back.width / 6, back.yMin + back.height / 6, back.width, back.height);
            FillRoundedRect(pixels, width, height, back, recipe.Fill, Mathf.RoundToInt(back.width * 0.08f));
            FillRoundedRect(pixels, width, height, front, recipe.Accent, Mathf.RoundToInt(front.width * 0.08f));
            StrokeRoundedRect(pixels, width, height, front, recipe.Stroke, Mathf.Max(2, front.width / 24), Mathf.RoundToInt(front.width * 0.08f));
        }

        private static void DrawPen(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            int thickness = Mathf.Max(8, Mathf.Min(canvas.width, canvas.height) / 8);
            Vector2Int start = new Vector2Int(canvas.xMin + canvas.width / 6, canvas.yMin + canvas.height / 3);
            Vector2Int end = new Vector2Int(canvas.xMax - canvas.width / 8, canvas.yMax - canvas.height / 3);
            DrawLine(pixels, width, height, start.x, start.y, end.x, end.y, recipe.Fill, thickness);
            DrawLine(pixels, width, height, start.x, start.y, end.x, end.y, recipe.Stroke, Mathf.Max(2, thickness / 5));
            FillTriangle(
                pixels,
                width,
                height,
                end,
                new Vector2Int(end.x - thickness, end.y + thickness / 2),
                new Vector2Int(end.x - thickness / 2, end.y - thickness),
                recipe.Accent);
        }

        private static void DrawEraser(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 5, canvas.height / 4);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.25f));
            RectInt stripe = new RectInt(body.xMin, CenterY(body), body.width, body.height / 3);
            FillRoundedRect(pixels, width, height, stripe, recipe.Accent, Mathf.RoundToInt(stripe.height * 0.35f));
        }

        private static void DrawRuler(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 10, canvas.height / 3);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.2f));
            for (int i = 1; i < 7; i++)
            {
                int x = body.xMin + i * body.width / 7;
                DrawLine(pixels, width, height, x, body.yMin + body.height / 4, x, body.yMax - body.height / 4, recipe.Stroke, Mathf.Max(2, body.height / 10));
            }
        }

        private static void DrawCorrectionTape(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 5, canvas.height / 5);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.width * 0.22f));
            int bodyCenterX = CenterX(body);
            int bodyCenterY = CenterY(body);
            DrawCircle(pixels, width, height, bodyCenterX - body.width / 6, bodyCenterY, Mathf.Max(4, body.height / 7), recipe.Accent);
            DrawLine(pixels, width, height, bodyCenterX, bodyCenterY, body.xMax - body.width / 7, body.yMin + body.height / 4, recipe.Accent, Mathf.Max(3, body.height / 8));
        }

        private static void DrawBatteries(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt left = new RectInt(canvas.xMin + canvas.width / 6, canvas.yMin + canvas.height / 4, canvas.width / 4, canvas.height / 2);
            RectInt right = new RectInt(CenterX(canvas) + canvas.width / 20, canvas.yMin + canvas.height / 4, canvas.width / 4, canvas.height / 2);
            DrawBatteryCell(pixels, width, height, left, recipe);
            DrawBatteryCell(pixels, width, height, right, recipe);
        }

        private static void DrawBandageBox(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 10, canvas.height / 4);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.25f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.height / 10), Mathf.RoundToInt(body.height * 0.25f));
            RectInt crossH = new RectInt(CenterX(body) - body.width / 5, CenterY(body) - body.height / 12, body.width * 2 / 5, body.height / 6);
            RectInt crossV = new RectInt(CenterX(body) - body.width / 12, CenterY(body) - body.height / 5, body.width / 6, body.height * 2 / 5);
            FillRoundedRect(pixels, width, height, crossH, recipe.Accent, Mathf.RoundToInt(crossH.height * 0.35f));
            FillRoundedRect(pixels, width, height, crossV, recipe.Accent, Mathf.RoundToInt(crossV.width * 0.35f));
        }

        private static void DrawToothbrush(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            int thickness = Mathf.Max(6, canvas.height / 9);
            int startX = canvas.xMin + canvas.width / 8;
            int endX = canvas.xMax - canvas.width / 6;
            int y = CenterY(canvas);
            DrawLine(pixels, width, height, startX, y, endX, y, recipe.Fill, thickness);
            RectInt head = new RectInt(endX - thickness, y - thickness, canvas.width / 8, thickness * 2);
            FillRoundedRect(pixels, width, height, head, recipe.Accent, Mathf.RoundToInt(thickness * 0.5f));
            for (int i = 0; i < 4; i++)
            {
                int x = head.xMin + i * head.width / 4;
                DrawLine(pixels, width, height, x, head.yMax - 2, x, head.yMax + thickness / 2, recipe.Stroke, 2);
            }
        }

        private static void DrawToothpaste(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 10, canvas.height / 4);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.2f));
            RectInt cap = new RectInt(body.xMax - body.width / 8, CenterY(body) - body.height / 4, body.width / 7, body.height / 2);
            FillRoundedRect(pixels, width, height, cap, recipe.Stroke, Mathf.RoundToInt(cap.height * 0.2f));
            RectInt stripe = new RectInt(CenterX(body) - body.width / 8, body.yMin, body.width / 4, body.height);
            FillRoundedRect(pixels, width, height, stripe, recipe.Accent, Mathf.RoundToInt(stripe.height * 0.15f));
        }

        private static void DrawSoap(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 6, canvas.height / 4);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.45f));
            DrawArcScore(pixels, width, height, body, recipe.Accent);
        }

        private static void DrawUmbrella(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            int y = CenterY(canvas) + canvas.height / 8;
            DrawLine(pixels, width, height, canvas.xMin + canvas.width / 5, y, canvas.xMax - canvas.width / 5, y, recipe.Fill, Mathf.Max(8, canvas.height / 10));
            FillTriangle(
                pixels,
                width,
                height,
                new Vector2Int(canvas.xMax - canvas.width / 5, y),
                new Vector2Int(canvas.xMax - canvas.width / 3, y + canvas.height / 7),
                new Vector2Int(canvas.xMax - canvas.width / 3, y - canvas.height / 7),
                recipe.Stroke);
            DrawLine(pixels, width, height, canvas.xMin + canvas.width / 4, y - canvas.height / 10, canvas.xMin + canvas.width / 6, canvas.yMin + canvas.height / 7, recipe.Accent, Mathf.Max(5, canvas.height / 14));
        }

        private static void DrawSanitizer(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = new RectInt(CenterX(canvas) - canvas.width / 6, canvas.yMin + canvas.height / 6, canvas.width / 3, canvas.height / 2);
            RectInt neck = new RectInt(CenterX(body) - body.width / 5, body.yMax - 1, body.width / 3, canvas.height / 10);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.width * 0.35f));
            FillRoundedRect(pixels, width, height, neck, recipe.Fill, Mathf.RoundToInt(neck.width * 0.25f));
            DrawLine(pixels, width, height, CenterX(neck), neck.yMax, CenterX(neck) + body.width / 2, neck.yMax + canvas.height / 12, recipe.Stroke, Mathf.Max(3, body.width / 12));
            RectInt drop = new RectInt(CenterX(body) - body.width / 8, CenterY(body) - body.height / 8, body.width / 4, body.height / 3);
            FillTriangle(
                pixels,
                width,
                height,
                new Vector2Int(CenterX(drop), drop.yMax),
                new Vector2Int(drop.xMin, drop.yMin + drop.height / 3),
                new Vector2Int(drop.xMax, drop.yMin + drop.height / 3),
                recipe.Accent);
            DrawCircle(pixels, width, height, CenterX(drop), drop.yMin + drop.height / 3, Mathf.Max(2, drop.width / 3), recipe.Accent);
        }

        private static void DrawHairTiePack(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 8, canvas.height / 5);
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.height * 0.2f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.height / 10), Mathf.RoundToInt(body.height * 0.2f));
            DrawRing(pixels, width, height, new Vector2Int(CenterX(body) - body.width / 6, CenterY(body)), body.height / 4, Mathf.Max(3, body.height / 10), recipe.Accent, recipe.Stroke);
            DrawRing(pixels, width, height, new Vector2Int(CenterX(body) + body.width / 6, CenterY(body)), body.height / 4, Mathf.Max(3, body.height / 10), recipe.Accent, recipe.Stroke);
        }

        private static void DrawBackpack(Color32[] pixels, int width, int height, RectInt canvas, IconRecipe recipe)
        {
            RectInt body = Inset(canvas, canvas.width / 5, canvas.height / 7);
            body.yMin += canvas.height / 8;
            body.height = Mathf.Max(1, body.height - canvas.height / 8);

            RectInt flap = new RectInt(
                body.xMin + body.width / 7,
                body.yMax - body.height / 3,
                body.width * 5 / 7,
                body.height / 3);
            RectInt pocket = new RectInt(
                body.xMin + body.width / 4,
                body.yMin + body.height / 7,
                body.width / 2,
                body.height / 4);

            DrawLine(pixels, width, height, body.xMin + body.width / 5, body.yMax - body.height / 10, body.xMin - body.width / 8, body.yMin + body.height / 3, recipe.Stroke, Mathf.Max(3, body.width / 12));
            DrawLine(pixels, width, height, body.xMax - body.width / 5, body.yMax - body.height / 10, body.xMax + body.width / 8, body.yMin + body.height / 3, recipe.Stroke, Mathf.Max(3, body.width / 12));
            FillRoundedRect(pixels, width, height, body, recipe.Fill, Mathf.RoundToInt(body.width * 0.16f));
            StrokeRoundedRect(pixels, width, height, body, recipe.Stroke, Mathf.Max(2, body.width / 18), Mathf.RoundToInt(body.width * 0.16f));
            FillRoundedRect(pixels, width, height, flap, Color32.Lerp(recipe.Fill, recipe.Stroke, 0.18f), Mathf.RoundToInt(flap.height * 0.28f));
            StrokeRoundedRect(pixels, width, height, flap, recipe.Stroke, Mathf.Max(2, flap.height / 9), Mathf.RoundToInt(flap.height * 0.28f));
            FillRoundedRect(pixels, width, height, pocket, recipe.Accent, Mathf.RoundToInt(pocket.height * 0.24f));
            StrokeRoundedRect(pixels, width, height, pocket, recipe.Stroke, Mathf.Max(2, pocket.height / 8), Mathf.RoundToInt(pocket.height * 0.24f));
            DrawLine(pixels, width, height, CenterX(body), body.yMin + body.height / 12, CenterX(body), body.yMax - body.height / 10, recipe.Stroke, Mathf.Max(2, body.width / 24));
        }

        private static void DrawBatteryCell(Color32[] pixels, int width, int height, RectInt rect, IconRecipe recipe)
        {
            FillRoundedRect(pixels, width, height, rect, recipe.Fill, Mathf.RoundToInt(rect.width * 0.2f));
            RectInt cap = new RectInt(CenterX(rect) - rect.width / 6, rect.yMax - 1, rect.width / 3, rect.height / 8);
            FillRect(pixels, width, height, cap, recipe.Stroke);
            RectInt stripe = new RectInt(rect.xMin + rect.width / 4, rect.yMin + rect.height / 4, rect.width / 2, rect.height / 5);
            FillRoundedRect(pixels, width, height, stripe, recipe.Accent, Mathf.RoundToInt(stripe.height * 0.35f));
        }

        private static void DrawRuleLines(Color32[] pixels, int width, int height, RectInt rect, Color32 color, int lineCount)
        {
            int stroke = Mathf.Max(2, rect.height / 18);
            for (int i = 0; i < lineCount; i++)
            {
                int y = rect.yMax - (i + 1) * rect.height / (lineCount + 1);
                DrawLine(pixels, width, height, rect.xMin, y, rect.xMax, y, color, stroke);
            }
        }

        private static void DrawArcScore(Color32[] pixels, int width, int height, RectInt rect, Color32 color)
        {
            int stroke = Mathf.Max(2, rect.height / 12);
            DrawLine(pixels, width, height, rect.xMin + rect.width / 4, CenterY(rect), CenterX(rect), rect.yMax - rect.height / 5, color, stroke);
            DrawLine(pixels, width, height, CenterX(rect), rect.yMax - rect.height / 5, rect.xMax - rect.width / 4, CenterY(rect), color, stroke);
        }

        private static void DrawRing(Color32[] pixels, int width, int height, Vector2Int center, int radius, int thickness, Color32 fill, Color32 stroke)
        {
            int outer = radius;
            int inner = Mathf.Max(1, radius - thickness);
            for (int y = center.y - outer; y <= center.y + outer; y++)
            {
                for (int x = center.x - outer; x <= center.x + outer; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= outer && distance >= inner)
                    {
                        SetPixel(pixels, width, height, x, y, fill);
                    }
                    else if (distance < inner && distance >= inner - Mathf.Max(2, thickness / 2))
                    {
                        SetPixel(pixels, width, height, x, y, stroke);
                    }
                }
            }
        }

        private static void Clear(Color32[] pixels)
        {
            Color32 transparent = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = transparent;
            }
        }

        private static RectInt Inset(RectInt rect, int horizontal, int vertical)
        {
            return new RectInt(
                rect.xMin + horizontal,
                rect.yMin + vertical,
                Mathf.Max(1, rect.width - horizontal * 2),
                Mathf.Max(1, rect.height - vertical * 2));
        }

        private static int CenterX(RectInt rect)
        {
            return rect.xMin + rect.width / 2;
        }

        private static int CenterY(RectInt rect)
        {
            return rect.yMin + rect.height / 2;
        }

        private static void FillRect(Color32[] pixels, int width, int height, RectInt rect, Color32 color)
        {
            int xMin = Mathf.Max(0, rect.xMin);
            int xMax = Mathf.Min(width, rect.xMax);
            int yMin = Mathf.Max(0, rect.yMin);
            int yMax = Mathf.Min(height, rect.yMax);
            for (int y = yMin; y < yMax; y++)
            {
                int row = y * width;
                for (int x = xMin; x < xMax; x++)
                {
                    pixels[row + x] = color;
                }
            }
        }

        private static void StrokeRect(Color32[] pixels, int width, int height, RectInt rect, Color32 color, int thickness)
        {
            FillRect(pixels, width, height, new RectInt(rect.xMin, rect.yMin, rect.width, thickness), color);
            FillRect(pixels, width, height, new RectInt(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            FillRect(pixels, width, height, new RectInt(rect.xMin, rect.yMin, thickness, rect.height), color);
            FillRect(pixels, width, height, new RectInt(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        private static void FillRoundedRect(Color32[] pixels, int width, int height, RectInt rect, Color32 color, int radius)
        {
            radius = Mathf.Clamp(radius, 0, Mathf.Min(rect.width, rect.height) / 2);
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (InsideRoundedRect(rect, x, y, radius))
                    {
                        SetPixel(pixels, width, height, x, y, color);
                    }
                }
            }
        }

        private static void StrokeRoundedRect(Color32[] pixels, int width, int height, RectInt rect, Color32 color, int thickness, int radius)
        {
            if (thickness <= 0)
            {
                return;
            }

            radius = Mathf.Clamp(radius, 0, Mathf.Min(rect.width, rect.height) / 2);
            RectInt inner = Inset(rect, thickness, thickness);
            int innerRadius = Mathf.Max(0, radius - thickness);

            int xMin = Mathf.Max(0, rect.xMin);
            int xMax = Mathf.Min(width, rect.xMax);
            int yMin = Mathf.Max(0, rect.yMin);
            int yMax = Mathf.Min(height, rect.yMax);
            for (int y = yMin; y < yMax; y++)
            {
                for (int x = xMin; x < xMax; x++)
                {
                    if (!InsideRoundedRect(rect, x, y, radius))
                    {
                        continue;
                    }

                    if (inner.width > 0 &&
                        inner.height > 0 &&
                        InsideRoundedRect(inner, x, y, innerRadius))
                    {
                        continue;
                    }

                    SetPixel(pixels, width, height, x, y, color);
                }
            }
        }

        private static bool InsideRoundedRect(RectInt rect, int x, int y, int radius)
        {
            if (radius <= 0)
            {
                return x >= rect.xMin && x < rect.xMax && y >= rect.yMin && y < rect.yMax;
            }

            int left = rect.xMin + radius;
            int right = rect.xMax - radius - 1;
            int bottom = rect.yMin + radius;
            int top = rect.yMax - radius - 1;

            if ((x >= left && x <= right) || (y >= bottom && y <= top))
            {
                return true;
            }

            Vector2 corner;
            if (x < left && y < bottom)
            {
                corner = new Vector2(left, bottom);
            }
            else if (x < left && y > top)
            {
                corner = new Vector2(left, top);
            }
            else if (x > right && y < bottom)
            {
                corner = new Vector2(right, bottom);
            }
            else
            {
                corner = new Vector2(right, top);
            }

            float dx = x - corner.x;
            float dy = y - corner.y;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static void DrawLine(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color32 color, int thickness)
        {
            Vector2 start = new Vector2(x0, y0);
            Vector2 end = new Vector2(x1, y1);
            float radius = Mathf.Max(1f, thickness * 0.5f);
            RectInt bounds = new RectInt(
                Mathf.FloorToInt(Mathf.Min(x0, x1) - radius),
                Mathf.FloorToInt(Mathf.Min(y0, y1) - radius),
                Mathf.CeilToInt(Mathf.Abs(x1 - x0) + radius * 2f),
                Mathf.CeilToInt(Mathf.Abs(y1 - y0) + radius * 2f));

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    float distance = DistancePointToSegment(new Vector2(x + 0.5f, y + 0.5f), start, end);
                    if (distance <= radius)
                    {
                        SetPixel(pixels, width, height, x, y, color);
                    }
                }
            }
        }

        private static void DrawCircle(Color32[] pixels, int width, int height, int centerX, int centerY, int radius, Color32 color)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSquared)
                    {
                        SetPixel(pixels, width, height, x, y, color);
                    }
                }
            }
        }

        private static void FillTriangle(Color32[] pixels, int width, int height, Vector2Int a, Vector2Int b, Vector2Int c, Color32 color)
        {
            int xMin = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            int xMax = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            int yMin = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            int yMax = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    if (PointInTriangle(new Vector2(x, y), a, b, c))
                    {
                        SetPixel(pixels, width, height, x, y, color);
                    }
                }
            }
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = Cross(b - a, c - a);
            float s = Cross(c - a, p - a) / area;
            float t = Cross(a - b, p - b) / area;
            float u = Cross(b - c, p - c) / area;
            return s >= 0f && t >= 0f && u >= 0f;
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float segmentSqr = segment.sqrMagnitude;
            if (segmentSqr <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentSqr);
            Vector2 projection = start + segment * t;
            return Vector2.Distance(point, projection);
        }

        private static void SetPixel(Color32[] pixels, int width, int height, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            pixels[y * width + x] = color;
        }

        private static Color32 ParseColor(string html)
        {
            ColorUtility.TryParseHtmlString(html, out Color color);
            return color;
        }

        private readonly struct IconRecipe
        {
            public IconRecipe(IconShape shape, Color32 fill, Color32 accent, Color32 stroke)
            {
                Shape = shape;
                Fill = fill;
                Accent = accent;
                Stroke = stroke;
            }

            public IconShape Shape { get; }

            public Color32 Fill { get; }

            public Color32 Accent { get; }

            public Color32 Stroke { get; }
        }
    }
}
