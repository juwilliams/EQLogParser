﻿using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace EQLogParser
{
  class LogReader
  {
    private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    public delegate void ParseLineCallback(string line);
    public delegate void InitialLoadCompleteCallback();
    public long FileSize = 0;
    public long BytesRead = 0;

    private string FileName;
    private ParseLineCallback LoadingCallback;
    private InitialLoadCompleteCallback CompleteCallback;
    private ThreadState LogThreadState;
    private bool MonitorOnly;

    public LogReader(string fileName, bool monitorOnly, ParseLineCallback loadingCallback, InitialLoadCompleteCallback completeCallback)
    {
      FileName = fileName;
      LoadingCallback = loadingCallback;
      CompleteCallback = completeCallback;
      MonitorOnly = monitorOnly;
    }

    public void Start()
    {
      if (LogThreadState != null)
      {
        LogThreadState.stop();
      }

      LogThreadState = new ThreadState();
      ThreadState myState = LogThreadState;

      new Thread(() =>
      {
        try
        {
          FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
          StreamReader reader = new StreamReader(fs, System.Text.Encoding.UTF8, true, 4096);

          string logFilePath = FileName.Substring(0, FileName.LastIndexOf("\\")) + "\\";
          string logFileName = FileName.Substring(FileName.LastIndexOf("\\") + 1);

          FileSize = fs.Length;

          if (MonitorOnly)
          {
            fs.Seek(FileSize, 0);
          }

          while (!reader.EndOfStream && myState.isRunning())
          {
            string line = reader.ReadLine();
            BytesRead += line.Length + 2; // EOL
            LoadingCallback(line);
          }

          CompleteCallback();
          BytesRead += 2; // EOF

          // setup watcher
          FileSystemWatcher fsw = new FileSystemWatcher
          {
            Path = logFilePath,
            Filter = logFileName
          };

          // events to notify for changes
          fsw.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

          fsw.EnableRaisingEvents = true;

          bool exitOnError = false;
          while (myState.isRunning() && !exitOnError)
          {
            WaitForChangedResult result = fsw.WaitForChanged(WatcherChangeTypes.Deleted | WatcherChangeTypes.Changed, 2000);

            // check if exit during wait period
            if (!myState.isRunning() || exitOnError)
            {
              break;
            }

            switch (result.ChangeType)
            {
              case WatcherChangeTypes.Deleted:
                // file gone
                exitOnError = true;
                break;
              case WatcherChangeTypes.Changed:
                if (reader != null)
                {
                  while (!reader.EndOfStream)
                  {
                    string line = reader.ReadLine();
                    LoadingCallback(line);
                  }
                }
                break;
            }
          }

          reader.Close();
          fsw.Dispose();
        }
        catch (Exception e)
        {
          LOG.Error(e);
        }
      }).Start();
    }

    public void Stop()
    {
      if (LogThreadState != null)
      {
        LogThreadState.stop();
      }
    }
  }

  public class ThreadState
  {
    private bool running = true;

    public void stop()
    {
      running = false;
    }

    public bool isRunning()
    {
      return running;
    }
  }
}
