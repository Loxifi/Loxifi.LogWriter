namespace Loxifi
{
    /// <summary>
    /// Determines where the log writer forwards string writes to
    /// </summary>
    [Flags]
    public enum LogOutput
    {
        /// <summary>
        /// Writes to Debug
        /// </summary>
        Debug = 1,

        /// <summary>
        /// Writes to Console
        /// </summary>
        Console = 2,

        /// <summary>
        /// Writes to disk
        /// </summary>
        File = 4,

        /// <summary>
        /// Writes to all targets
        /// </summary>
        All = Debug | Console | File
    }
}