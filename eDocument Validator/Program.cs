using eDocument_Validator.Forms;
using System;
using System.Windows.Forms;

namespace eDocument_Validator
{
    internal static class Program
    {
        /// <summary>
        /// The starting point of the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
