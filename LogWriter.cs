using System.Diagnostics;
using System.Reflection;

namespace Loxifi.LogWriter
{
    /// <summary>
    /// A class that asynchronously writes provided strings to specified targets allowing for writing a large
    /// amount of data with little to no slowdown.
    /// </summary>
    public class LogWriter : IDisposable
    {
        private static void ConsoleWriteLine(string s) => Console.WriteLine(s);

        private static void DebugWriteLine(string s) => Debug.WriteLine(s);

        private static readonly AsyncStringWriter _consoleQueue = new(ConsoleWriteLine);
        private static readonly AsyncStringWriter _debugQueue = new(DebugWriteLine);
        private AsyncStringWriter? _fileQueue;
        private CompressedFileWriter? _fileWriter;

        private readonly LogWriterSettings _settings;

        private bool _disposedValue;

        /// <summary>
        /// The Directory that any log files are written to
        /// </summary>
        public string Directory => _settings.Directory;

        /// <summary>
        /// Log file full path and file name
        /// </summary>
        public string LogFileFullName => Path.Combine(_settings.Directory, LogFileName);

        /// <summary>
        /// Constructs a new instance of the log writer
        /// </summary>
        /// <summary>
        /// The name of the file the (if any) disk stream is being written to
        /// </summary>
        public string LogFileName { get; private set; } = $"{DateTime.Now:yyyyMMdd_HHmmss}_{AssemblyName}.log";

        /// <summary>
        /// When writing to the debug window, the name of the output that will be used.
        /// </summary>
        public string DebugCategory
        {
            get => _debugCategory ?? LogFileName;
            set => _debugCategory = value;
        }

        private string? _debugCategory;

        private static string AssemblyName
        {
            get
            {
                string toReturn = "Unknown";

                try
                {
                    toReturn = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }

                return toReturn;
            }
        }

        /// <summary>
        /// Constructs a new instance of the log file writer with the given settings
        /// </summary>
        /// <param name="settings">Any settings to overwrite from default</param>
        public LogWriter(LogWriterSettings? settings = null)
        {
            _settings = settings ?? new LogWriterSettings();

            LogFileName = _settings.LogFileName ?? LogFileName;

            //Create output directory if it doesn't exist
            if (!System.IO.Directory.Exists(_settings.Directory))
            {
                _ = System.IO.Directory.CreateDirectory(_settings.Directory);
            }

            if (_settings.OutputTarget.HasFlag(LogOutput.File))
            {
                InitFileQueue();
            }

            //https://stackoverflow.com/questions/18020861/how-to-get-notified-before-static-variables-are-finalized
            //Catch domain shutdown (Hack: frantically look for things we can catch)
            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                AppDomain.CurrentDomain.ProcessExit += MyTerminationHandler;
            }
            else
            {
                AppDomain.CurrentDomain.DomainUnload += MyTerminationHandler;
            }
        }

        private void MyTerminationHandler(object sender, EventArgs e) => Dispose();

        private void InitFileQueue()
        {
            _fileWriter = new CompressedFileWriter(LogFileFullName, _settings.Compression);
            _fileQueue = new AsyncStringWriter(_fileWriter.WriteLine);
        }

        /// <summary>
        /// Disposes of the writer and flushes to disk
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Flushes the disk output streamwriter
        /// </summary>
        public Task Flush()
        {
            return Task.Run(() =>
            {
                if (_fileQueue != null)
                {
                    _fileQueue.WaitForFlush();
                    _fileWriter?.Flush();
                }
            });
        }

        /// <summary>
        /// Writes an object or message to the log targets
        /// </summary>
        /// <param name="toLog">The object or message to log</param>
        /// <param name="target">Specific target for this output. If null, uses instance default</param>
        public void WriteLine(object toLog, LogOutput? target = null)
        {
            //Time stamp it
            string prepend = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ";

            //Add the time stamp to the string
            string logString = $"{prepend} {SerializeObject(toLog)}";

            target ??= _settings.OutputTarget;
            if (target.Value.HasFlag(LogOutput.File))
            {
                if (_fileQueue is null)
                {
                    InitFileQueue();
                }

                //To the file
                _fileQueue?.Enqueue(logString);
            }

            if (target.Value.HasFlag(LogOutput.Debug))
            {
                _debugQueue.Enqueue(logString);
            }

            if (target.Value.HasFlag(LogOutput.Console))
            {
                _consoleQueue.Enqueue(logString);
            }
        }

        /// <summary>
        /// Disposes of the writer and flushes to disk
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;

                _fileQueue?.Dispose();
                _fileWriter?.Dispose();
                _consoleQueue.Dispose();
                _debugQueue.Dispose();
            }
            else
            {
                Debug.WriteLine($"Attempting to dispose of already disposed {typeof(LogWriter)}");
            }
        }

        private string SerializeObject(object toSerialize)
        {
            if (toSerialize is string s)
            {
                return s;
            }

            if (_settings.ObjectSerializationOverride is null)
            {
                return $"{toSerialize}";
            }

            switch (_settings.ObjectSerializationMethod)
            {
                case LogSerializationMethod.ToString:
                    return $"{toSerialize}";

                case LogSerializationMethod.Override:
                    return _settings.ObjectSerializationOverride(toSerialize);

                case LogSerializationMethod.Auto:
                    if (toSerialize is null)
                    {
                        return string.Empty;
                    }

                    MethodInfo mi = toSerialize.GetType().GetMethod("ToString");

                    return mi.DeclaringType == typeof(object) ? _settings.ObjectSerializationOverride(toSerialize) : $"{toSerialize}";

                default:
                    throw new NotImplementedException($"{nameof(_settings.ObjectSerializationMethod)} unimplemented value {_settings.ObjectSerializationMethod}");
            }
        }
    }
}