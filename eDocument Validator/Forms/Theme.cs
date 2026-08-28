using System.Drawing;

namespace eDocument_Validator.Forms
{
    /// <summary>
    /// All colours the window uses, kept in one place so that a light and a dark
    /// appearance can be swapped without changing any drawing code.
    /// <para>
    /// The two sets are not simply inverted. On a dark background a saturated red
    /// becomes hard to read, so the dark set uses lighter, less saturated versions
    /// of the same colours, and the coloured backgrounds behind the headline are
    /// darkened instead of lightened.
    /// </para>
    /// </summary>
    public class Theme
    {
        /// <summary>Background of the window and of the result area.</summary>
        public Color Background { get; private set; }

        /// <summary>Background of the panels holding the controls.</summary>
        public Color PanelBackground { get; private set; }

        /// <summary>Background of text boxes and lists the user types into.</summary>
        public Color InputBackground { get; private set; }

        /// <summary>Colour of ordinary text.</summary>
        public Color Text { get; private set; }

        /// <summary>Colour of headings and of the application name.</summary>
        public Color Heading { get; private set; }

        /// <summary>Colour of labels and other text of secondary importance.</summary>
        public Color Quiet { get; private set; }

        public Color Error { get; private set; }
        public Color Warning { get; private set; }
        public Color Success { get; private set; }
        public Color Information { get; private set; }

        public Color ErrorBackground { get; private set; }
        public Color WarningBackground { get; private set; }
        public Color SuccessBackground { get; private set; }

        /// <summary>Border of buttons and input controls.</summary>
        public Color Border { get; private set; }

        /// <summary>Background of buttons.</summary>
        public Color ButtonBackground { get; private set; }

        /// <summary>True when this is the dark appearance.</summary>
        public bool IsDark { get; private set; }

        private Theme()
        {
        }

        public static readonly Theme Light = new Theme
        {
            IsDark = false,
            Background = Color.White,
            PanelBackground = Color.White,
            InputBackground = Color.White,
            Text = Color.FromArgb(30, 30, 30),
            Heading = Color.FromArgb(20, 20, 20),
            Quiet = Color.FromArgb(120, 120, 120),
            Error = Color.FromArgb(176, 0, 32),
            Warning = Color.FromArgb(150, 90, 0),
            Success = Color.FromArgb(0, 115, 60),
            Information = Color.FromArgb(95, 95, 95),
            ErrorBackground = Color.FromArgb(253, 235, 235),
            WarningBackground = Color.FromArgb(253, 246, 227),
            SuccessBackground = Color.FromArgb(232, 246, 237),
            Border = Color.FromArgb(200, 200, 200),
            ButtonBackground = Color.FromArgb(243, 243, 243)
        };

        public static readonly Theme Dark = new Theme
        {
            IsDark = true,
            Background = Color.FromArgb(30, 30, 30),
            PanelBackground = Color.FromArgb(37, 37, 38),
            InputBackground = Color.FromArgb(51, 51, 55),
            Text = Color.FromArgb(228, 228, 228),
            Heading = Color.FromArgb(242, 242, 242),
            Quiet = Color.FromArgb(150, 150, 150),
            Error = Color.FromArgb(244, 122, 133),
            Warning = Color.FromArgb(232, 178, 88),
            Success = Color.FromArgb(106, 208, 142),
            Information = Color.FromArgb(190, 190, 190),
            ErrorBackground = Color.FromArgb(62, 32, 36),
            WarningBackground = Color.FromArgb(58, 48, 30),
            SuccessBackground = Color.FromArgb(30, 56, 42),
            Border = Color.FromArgb(70, 70, 74),
            ButtonBackground = Color.FromArgb(58, 58, 62)
        };

        public static Theme For(bool useDark)
        {
            return useDark ? Dark : Light;
        }
    }
}
