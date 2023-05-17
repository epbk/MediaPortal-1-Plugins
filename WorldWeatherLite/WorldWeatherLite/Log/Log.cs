using System;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;
using System.Collections.Generic;
using MediaPortal.Profile;
using MediaPortal.Configuration;
using MediaPortal.Services;
using System.Runtime.CompilerServices;

namespace MediaPortal.Plugins.WorldWeatherLite.Logging
{
    internal static class Log
    {
        private const string _LOG_FILE_NAME_PREFIX = "WorldWeatherLite"; //GUIWorldWeaterLite.PLUGIN_NAME;
        private const string _LOG_FILE_NAME = _LOG_FILE_NAME_PREFIX + ".log";
        private const string _LOG_FILE_NAME_ERROR = _LOG_FILE_NAME_PREFIX + ".Error.log";
        private const string _LOG_FILE_NAME_OLD = _LOG_FILE_NAME_PREFIX + ".old.log";
        private const string _LOG_PATTERN = "MediaPortal.Plugins." + _LOG_FILE_NAME_PREFIX + ".";
        private const string _LOG_ARCHIVE_SUBFOLDER = "LogArchive";

        private const string _LOG_FILE_LAYOUT = "${date:format=dd-MMM-yyyy HH\\:mm\\:ss\\.fff} ${level:fixedLength=true:padding=5} "
                + "[${logger:fixedLength=true:padding=20:shortName=true}]: ${message} "
                + "${exception:format=tostring}";

        static Log()
        {
            Init();
        }

        /// <summary>
        /// Get or set current log level
        /// </summary>
        public static LogLevel LogLevel
        {
            get
            {
                return _LogLevel;
            }
            set
            {
                foreach (LoggingRule rule in _LogConfig.LoggingRules)
                {
                    if (!rule.LoggerNamePattern.StartsWith(_LOG_PATTERN) || rule.Targets[0] == _TargetError)
                        continue;

                    for (int i = 0; i <= 5; ++i)
                    {
                        LogLevel level = LogLevel.FromOrdinal(i);
                        if (level < value)
                            rule.DisableLoggingForLevel(level);
                        else
                            rule.EnableLoggingForLevel(level);
                    }
                }

                LogManager.ReconfigExistingLoggers();

                _LogLevel = value;
            }
        }private static LogLevel _LogLevel;

        internal static string LogFile
        {
            get
            {
                return _Target.FileName;
            }
        }

        private static FileTarget _Target;
        private static FileTarget _TargetError;

        private static LoggingConfiguration _LogConfig
        {
            get
            {
                if (_Target == null)
                    Init();

                return LogManager.Configuration;
            }
        }

        /// <summary>
        /// Initialize the logger
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static void Init()
        {
            if (_Target == null)
            {
                LogLevel logLevel = LogLevel.Debug;
                using (MPSettings xmlreader = new MPSettings())
                {
                    int iLvl;
                    string strLogLevel = xmlreader.GetValueAsString("general", "loglevel", "2"); // 0:error, 1:warn, 2:info, 3:debug,
                    if (int.TryParse(strLogLevel, out iLvl))
                    {
                        switch (iLvl)
                        {
                            case 0:
                                logLevel = LogLevel.Error;
                                break;

                            case 1:
                                logLevel = LogLevel.Warn;
                                break;

                            case 2:
                                logLevel = LogLevel.Info;
                                break;

                            case 3:
                            default:
                                logLevel = LogLevel.Debug;
                                break;
                        }
                    }
                }

                _LogLevel = logLevel;
                initLogger();
            }
        }

        private static void initLogger()
        {
            if ((LogManager.Configuration == null))
                LogManager.Configuration = new LoggingConfiguration();

            _Target = new FileTarget();
            _Target.Name = "Log";
            _Target.FileName = Config.GetFile(Config.Dir.Log, _LOG_FILE_NAME);
            _Target.Layout = _LOG_FILE_LAYOUT;
            _Target.ArchiveFileName = Config.GetFile(Config.Dir.Log, _LOG_ARCHIVE_SUBFOLDER, _LOG_FILE_NAME_PREFIX + ".{#}.log");
            _Target.ArchiveEvery = FileTarget.ArchiveEveryMode.Day;
            _Target.MaxArchiveFiles = 10;
            _Target.ArchiveNumbering = FileTarget.ArchiveNumberingMode.Sequence;
            LogManager.Configuration.AddTarget("Log", _Target);

            _TargetError = new FileTarget();
            _TargetError.Name = "LogError";
            _TargetError.FileName = Config.GetFile(Config.Dir.Log, _LOG_FILE_NAME_ERROR);
            _TargetError.Layout = _LOG_FILE_LAYOUT;
            _TargetError.DeleteOldFileOnStartup = false;
            LogManager.Configuration.AddTarget("LogError", _TargetError);

            //Default log rule
            AddRule(_LOG_PATTERN + "*");
        }

        internal static void AddRule(string strPattern)
        {
            _LogConfig.LoggingRules.Add(new LoggingRule(strPattern, _LogLevel, _Target));
            _LogConfig.LoggingRules.Add(new LoggingRule(strPattern, LogLevel.Error, _TargetError));
            LogManager.Configuration = LogManager.Configuration;
        }
    }
}
