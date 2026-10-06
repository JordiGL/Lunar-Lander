using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Fuente vectorial de trazos para texto estilo arcade (estilo monitor XY de los 70).
    ///
    /// Cada glifo se define sobre una cuadrícula de 4 x 6 como una lista de trazos separados
    /// por '|'. Cada trazo es una polilínea de puntos "x,y" (x: 0..4 izquierda-derecha,
    /// y: 0..6 abajo-arriba). Para añadir o retocar un carácter basta con editar su cadena en
    /// BuildGlyphs().
    ///
    /// La fuente devuelve los puntos normalizados (0..1 en ambos ejes); quien dibuja los
    /// escala a la altura de carácter que necesite (ver VectorHUD).
    /// </summary>
    public static class VectorFont
    {
        // Flechas para indicar la dirección de la velocidad.
        public const char ArrowUp = '↑';
        public const char ArrowDown = '↓';
        public const char ArrowLeft = '←';
        public const char ArrowRight = '→';

        public const float GridWidth = 4f;
        public const float GridHeight = 6f;

        /// <summary>Relación ancho/alto de un carácter.</summary>
        public const float Aspect = GridWidth / GridHeight;

        // El orden de estos campos importa: Glyphs se construye el último.
        private static readonly Vector2[][] Empty = new Vector2[0][];
        private static readonly Vector2[][] Fallback = Parse("0,0 0,6 4,6 4,0 0,0");
        private static readonly Dictionary<char, Vector2[][]> Glyphs = BuildGlyphs();

        /// <summary>
        /// Trazos de un carácter con coordenadas normalizadas (0..1). Los caracteres desconocidos
        /// devuelven un rectángulo; el espacio devuelve cero trazos. No modificar el resultado:
        /// es compartido.
        /// </summary>
        public static Vector2[][] GetStrokes(char c)
        {
            if (c == ' ') return Empty;

            // Intentar primero con el carácter original (para soportar minúsculas si existen)
            if (Glyphs.TryGetValue(c, out Vector2[][] strokes)) return strokes;

            // Si no, intentar con la versión en mayúsculas
            c = char.ToUpperInvariant(c);
            return Glyphs.TryGetValue(c, out strokes) ? strokes : Fallback;
        }

        /// <summary>
        /// Calcula el ancho total de una cadena en unidades de la rejilla.
        /// </summary>
        public static float GetTextWidth(string text, float spacing)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return text.Length * GridWidth + (text.Length - 1) * spacing;
        }


        private static Dictionary<char, Vector2[][]> BuildGlyphs()
        {
            var g = new Dictionary<char, Vector2[][]>();

            // ---- Letras (esquinas achaflanadas para el aspecto de monitor vectorial) ----
            g['A'] = Parse("0,0 0,4 2,6 4,4 4,0 | 0,3 4,3");
            g['B'] = Parse("0,0 0,6 3,6 4,5 4,4 3,3 0,3 | 3,3 4,2 4,1 3,0 0,0");
            g['C'] = Parse("4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1");
            g['D'] = Parse("0,0 0,6 2,6 4,4 4,2 2,0 0,0");
            g['E'] = Parse("4,6 0,6 0,0 4,0 | 0,3 3,3");
            g['F'] = Parse("0,0 0,6 4,6 | 0,3 3,3");
            g['G'] = Parse("4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,3 2,3");
            g['H'] = Parse("0,0 0,6 | 4,0 4,6 | 0,3 4,3");
            g['I'] = Parse("1,6 3,6 | 2,6 2,0 | 1,0 3,0");
            g['J'] = Parse("4,6 4,1 3,0 1,0 0,1");
            g['K'] = Parse("0,0 0,6 | 4,6 0,3 4,0");
            g['L'] = Parse("0,6 0,0 4,0");
            g['M'] = Parse("0,0 0,6 2,3 4,6 4,0");
            g['N'] = Parse("0,0 0,6 4,0 4,6");
            g['O'] = Parse("1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0");
            g['P'] = Parse("0,0 0,6 3,6 4,5 4,4 3,3 0,3");
            g['Q'] = Parse("1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0 | 2,2 4,0");
            g['R'] = Parse("0,0 0,6 3,6 4,5 4,4 3,3 0,3 | 2,3 4,0");
            g['S'] = Parse("4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1");
            g['T'] = Parse("0,6 4,6 | 2,6 2,0");
            g['U'] = Parse("0,6 0,1 1,0 3,0 4,1 4,6");
            g['V'] = Parse("0,6 2,0 4,6");
            g['W'] = Parse("0,6 1,0 2,3 3,0 4,6");
            g['X'] = Parse("0,6 4,0 | 4,6 0,0");
            g['Y'] = Parse("0,6 2,3 4,6 | 2,3 2,0");
            g['Z'] = Parse("0,6 4,6 0,0 4,0");

            // ---- Minúsculas y Decoración ----
            g['x'] = Parse("0,1 4,4 | 0,4 4,1"); // 'x' estilo subíndice
            g['['] = Parse("3,6 1,6 1,0 3,0");
            g[']'] = Parse("1,6 3,6 3,0 1,0");

            // ---- Dígitos ----


            g['0'] = Parse("1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0");
            g['1'] = Parse("1,5 2,6 2,0 | 1,0 3,0");
            g['2'] = Parse("0,5 1,6 3,6 4,5 4,4 0,0 4,0");
            g['3'] = Parse("0,5 1,6 3,6 4,5 4,4 3,3 1,3 | 3,3 4,2 4,1 3,0 1,0 0,1");
            g['4'] = Parse("3,0 3,6 0,2 4,2");
            g['5'] = Parse("4,6 0,6 0,3 3,3 4,2 4,1 3,0 0,0");
            g['6'] = Parse("4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,2 3,3 0,3");
            g['7'] = Parse("0,6 4,6 1,0");
            g['8'] = Parse("1,3 0,4 0,5 1,6 3,6 4,5 4,4 3,3 1,3 0,2 0,1 1,0 3,0 4,1 4,2 3,3");
            g['9'] = Parse("4,3 1,3 0,4 0,5 1,6 3,6 4,5 4,1 3,0 1,0 0,1");

            // ---- Puntuación y símbolos ----
            g['-'] = Parse("0,3 4,3");
            g['+'] = Parse("0,3 4,3 | 2,1 2,5");
            g['.'] = Parse("2,0 2,1");
            g[':'] = Parse("2,1 2,2 | 2,4 2,5");
            g['/'] = Parse("0,0 4,6");
            g['<'] = Parse("4,6 0,3 4,0");
            g['>'] = Parse("0,6 4,3 0,0");
            g['!'] = Parse("2,6 2,2 | 2,0 2,1");

            // ---- Flechas ----
            g[ArrowUp] = Parse("2,0 2,6 | 0,4 2,6 4,4");
            g[ArrowDown] = Parse("2,6 2,0 | 0,2 2,0 4,2");
            g[ArrowLeft] = Parse("4,3 0,3 | 2,5 0,3 2,1");
            g[ArrowRight] = Parse("0,3 4,3 | 2,5 4,3 2,1");

            return g;
        }

        /// <summary>Convierte "x,y x,y | x,y x,y" en trazos con coordenadas normalizadas (0..1).</summary>
        private static Vector2[][] Parse(string definition)
        {
            string[] strokeDefs = definition.Split('|');
            var strokes = new Vector2[strokeDefs.Length][];

            for (int i = 0; i < strokeDefs.Length; i++)
            {
                string[] pointDefs = strokeDefs[i].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                var points = new Vector2[pointDefs.Length];

                for (int j = 0; j < pointDefs.Length; j++)
                {
                    string[] xy = pointDefs[j].Split(',');
                    float x = float.Parse(xy[0], CultureInfo.InvariantCulture);
                    float y = float.Parse(xy[1], CultureInfo.InvariantCulture);
                    points[j] = new Vector2(x / GridWidth, y / GridHeight);
                }

                strokes[i] = points;
            }

            return strokes;
        }
    }
}