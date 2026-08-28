namespace eDocument_Validator.Validation
{
    /// <summary>
    /// How serious a single validation message is.
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// The document is correct here. Used to record what was checked, so a
        /// clean document still shows evidence of the work that was done.
        /// </summary>
        Information = 0,

        /// <summary>
        /// The document can still be processed, but it does not follow a
        /// recommendation. The receiver will most likely accept it.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// The document breaks a rule. The receiver is expected to reject it.
        /// </summary>
        Error = 2
    }
}
