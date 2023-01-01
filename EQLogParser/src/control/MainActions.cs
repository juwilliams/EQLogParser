﻿using Microsoft.Win32;
using Syncfusion.SfSkinManager;
using Syncfusion.Themes.MaterialLight.WPF;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.Windows.Tools.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EQLogParser
{
  class MainActions
  {
    private const string PETS_LIST_TITLE = "Verified Pets ({0})";
    private const string PLAYER_LIST_TITLE = "Verified Players ({0})";
    private static readonly ObservableCollection<dynamic> VerifiedPlayersView = new ObservableCollection<dynamic>();
    private static readonly ObservableCollection<dynamic> VerifiedPetsView = new ObservableCollection<dynamic>();
    private static readonly ObservableCollection<PetMapping> PetPlayersView = new ObservableCollection<PetMapping>();
    private static readonly SortablePetMappingComparer TheSortablePetMappingComparer = new SortablePetMappingComparer();
    private static readonly SortableNameComparer TheSortableNameComparer = new SortableNameComparer();
    private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    internal static void CheckGina(string action, double dateTime)
    {
      // if GINA data is recent then try to handle it
      if (action.IndexOf("{GINA:", StringComparison.OrdinalIgnoreCase) is int index && index > -1 && 
        (DateTime.Now - DateUtil.FromDouble(dateTime)).TotalSeconds <= 20 && action.IndexOf("}", index + 40) is int end && end > index)
      {
        return;

        Task.Delay(1000).ContinueWith(task =>
        {
          try
          {
            var ginaKey = action.Substring(index + 6, end - index - 6);
            var postData = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body><DownloadPackageChunk xmlns=\"http://tempuri.org/\"><sessionId>" +
              ginaKey + "</sessionId><chunkNumber>0</chunkNumber></DownloadPackageChunk></s:Body></s:Envelope>";

            var content = new StringContent(postData, UnicodeEncoding.UTF8, "text/xml");
            content.Headers.Add("Content-Length", postData.Length.ToString());

            var message = new HttpRequestMessage(HttpMethod.Post, @"http://eq.gimasoft.com/GINAServices/Package.svc");
            message.Content = content;
            message.Headers.Add("SOAPAction", "http://tempuri.org/IPackageService/DownloadPackageChunk");
            message.Headers.Add("Accept-Encoding", "gzip, deflate");

            var client = new HttpClient();
            var response = client.Send(message);
            if (response.IsSuccessStatusCode)
            {
              using (var data = response.Content.ReadAsStreamAsync())
              {
                data.Wait();

                var buffer = new byte[data.Result.Length];
                var read = data.Result.ReadAsync(buffer, 0, buffer.Length);
                read.Wait();

                using (var bufferStream = new MemoryStream(buffer))
                {
                  using (var gzip = new GZipStream(bufferStream, CompressionMode.Decompress))
                  {
                    using (var memory = new MemoryStream())
                    {
                      gzip.CopyTo(memory);
                      var xml = Encoding.UTF8.GetString(memory.ToArray());

                      if (!string.IsNullOrEmpty(xml) && xml.IndexOf("<a:ChunkData>") is int start && start > -1 && xml.IndexOf("</a:ChunkData>") is int end &&
                        end > start)
                      {
                        var encoded = xml.Substring(start + 13, end - start - 13);
                        var decoded = Convert.FromBase64String(encoded);

                        using (var zip = new ZipArchive(new MemoryStream(decoded), ZipArchiveMode.Read))
                        {
                          var entry = zip.Entries.First();
                          using (StreamReader sr = new StreamReader(entry.Open()))
                          {
                            var triggers = sr.ReadToEnd();
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            else
            {
              LOG.Error("Error Downloading GINA Triggers. Received Status Code = " + response.StatusCode.ToString());
            }

            client.Dispose();
          }
          catch (Exception ex)
          {
            LOG.Error("Error Downloading GINA Triggers", ex);
          }
        });
      }
    }

    internal static void CheckVersion(string version, TextBlock errorText)
    {
      var dispatcher = Application.Current.Dispatcher;
      Task.Delay(2000).ContinueWith(task =>
      {
        try
        {
          var client = new HttpClient();
          var request = client.GetStringAsync(@"https://github.com/kauffman12/EQLogParser/blob/master/README.md");
          request.Wait();

          var matches = new Regex(@"EQLogParser-(.*).msi""").Match(request.Result);
          if (matches.Success && matches.Groups.Count > 1 && !version.Equals(matches.Groups[1].Value) &&
            matches.Groups[1].Value is string updated && new Regex(@"\d.\d.\d").Match(updated).Success)
          {
            dispatcher.InvokeAsync(() =>
            {
              var msg = new MessageWindow("Version " + updated + " is Available. Download and Install?",
                EQLogParser.Resource.CHECK_VERSION, true);
              msg.ShowDialog();

              if (msg.IsYesClicked)
              {
                var url = "https://github.com/kauffman12/EQLogParser/raw/master/Release/EQLogParser-" + updated + ".msi";

                try
                {
                  using (var download = client.GetStreamAsync(url))
                  {
                    download.Wait();

                    var path = System.Environment.ExpandEnvironmentVariables("%userprofile%\\Downloads");
                    if (!Directory.Exists(path))
                    {
                      new MessageWindow("Unable to Access Downloads Folder. Need to Download Manually.",
                        EQLogParser.Resource.CHECK_VERSION).ShowDialog();
                      return;
                    }

                    path = path + "\\AutoUpdateEQLogParser";
                    if (!Directory.Exists(path))
                    {
                      Directory.CreateDirectory(path);
                    }

                    var fullPath = path + "\\EQLogParser-" + updated + ".msi";
                    using (var fs = new FileStream(fullPath, FileMode.Create))
                    {
                      download.Result.CopyTo(fs);
                    }

                    if (File.Exists(fullPath))
                    {
                      var process = Process.Start("msiexec", "/i \"" + fullPath + "\"");
                      if (!process.HasExited)
                      {
                        Task.Delay(1000).ContinueWith(task =>
                        {
                          dispatcher.InvokeAsync(() => Application.Current.MainWindow.Close());
                        });
                      }
                    }
                  }
                }
                catch (Exception ex2)
                {
                  new MessageWindow("Problem Install Updates. Check Error Log for Details.", EQLogParser.Resource.CHECK_VERSION).ShowDialog();
                  LOG.Error("Error Installing Updates", ex2);
                }
              }
            });
          }

          client.Dispose();
        }
        catch (Exception ex)
        {
          LOG.Error("Error Checking for Updates", ex);
          Application.Current.Dispatcher.InvokeAsync(() =>
          {
            errorText.Text = "Update Check Failed. Firewall?";
          });
        }
      });
    }

    internal static void Cleanup()
    {
      try
      {
        var path = System.Environment.ExpandEnvironmentVariables("%userprofile%\\Downloads");
        if (!Directory.Exists(path))
        {
          return;
        }

        path = path + "\\AutoUpdateEQLogParser";
        if (Directory.Exists(path))
        {
          foreach (var file in Directory.GetFiles(path))
          {
            var test = Path.GetFileName(file).Trim();
            if (test.StartsWith("EQLogParser") && test.EndsWith(".msi"))
            {
              File.Delete(file);
            }
          }
        }
      }
      catch (Exception _)
      {
        // ignore
      }
    }

    internal static void SetTheme(Window window, string theme)
    {
      if (window != null)
      {
        if (theme == "MaterialLight")
        {
          SfSkinManager.SetTheme(window, new Theme("MaterialLight"));
        }
        else
        {
          SfSkinManager.SetTheme(window, new Theme("MaterialDarkCustom;MaterialDark"));
        }
      }
    }

    internal static void LoadTheme(MainWindow window, string theme)
    {
      if (theme == "MaterialLight")
      {
        var themeSettings = new MaterialLightThemeSettings();
        themeSettings.PrimaryBackground = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF343434") };
        SfSkinManager.RegisterThemeSettings("MaterialLight", themeSettings);
        Application.Current.Resources["EQGoodForegroundBrush"] = new SolidColorBrush { Color = Colors.DarkGreen };
        Application.Current.Resources["EQMenuIconBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF3d7baf") };
        Application.Current.Resources["EQSearchBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FFa7baab") };
        Application.Current.Resources["EQWarnBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FFeaa6ac") };
        Application.Current.Resources["EQWarnForegroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FFb02021") };
        SfSkinManager.SetTheme(window, new Theme("MaterialLight"));
        Helpers.LoadDictionary("/Syncfusion.Themes.MaterialLight.WPF;component/MSControl/CheckBox.xaml");
        Helpers.LoadDictionary("/Syncfusion.Themes.MaterialLight.WPF;component/SfDataGrid/SfDataGrid.xaml");
        Helpers.LoadDictionary("/Syncfusion.Themes.MaterialLight.WPF;component/Common/Brushes.xaml");
        window.BorderBrush = Application.Current.Resources["ContentBackgroundAlt2"] as SolidColorBrush;

        if (!string.IsNullOrEmpty(window.statusText?.Text))
        {
          window.statusText.Foreground = Application.Current.Resources["EQGoodForegroundBrush"] as SolidColorBrush;
        }
      }
      else
      {
        Application.Current.Resources["EQGoodForegroundBrush"] = new SolidColorBrush { Color = Colors.LightGreen };
        Application.Current.Resources["EQMenuIconBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF4F9FE2") };
        Application.Current.Resources["EQSearchBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF314435") };
        Application.Current.Resources["EQWarnBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF96410d") };
        Application.Current.Resources["EQWarnForegroundBrush"] = new SolidColorBrush { Color = Colors.Orange };
        SfSkinManager.SetTheme(window, new Theme("MaterialDarkCustom;MaterialDark"));
        Helpers.LoadDictionary("/Syncfusion.Themes.MaterialDarkCustom.WPF;component/MSControl/CheckBox.xaml");
        Helpers.LoadDictionary("/Syncfusion.Themes.MaterialDarkCustom.WPF;component/SfDataGrid/SfDataGrid.xaml");
        Helpers.LoadDictionary("/Syncfusion.Themes.MaterialDarkCustom.WPF;component/Common/Brushes.xaml");
        window.BorderBrush = Application.Current.Resources["ContentBackgroundAlt2"] as SolidColorBrush;

        if (!string.IsNullOrEmpty(window.statusText?.Text))
        {
          window.statusText.Foreground = Application.Current.Resources["EQGoodForegroundBrush"] as SolidColorBrush;
        }
      }

      // common
      Application.Current.Resources["OverlayActiveBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF191919") };
      Application.Current.Resources["OverlayConfigBrush"] = Application.Current.Resources["ContentBackgroundAlt2"];
      Application.Current.Resources["OverlayCurrentBrush"] = Application.Current.Resources["OverlayActiveBrush"];
    }

    internal static void Clear(ContentControl petsWindow, ContentControl playersWindow)
    {
      PetPlayersView.Clear();
      VerifiedPetsView.Clear();
      VerifiedPlayersView.Clear();

      var entry = new ExpandoObject() as dynamic;
      entry.Name = Labels.UNASSIGNED;
      VerifiedPlayersView.Add(entry);
      DockingManager.SetHeader(petsWindow, string.Format(PETS_LIST_TITLE, VerifiedPetsView.Count));
      DockingManager.SetHeader(playersWindow, string.Format(PLAYER_LIST_TITLE, VerifiedPlayersView.Count));
    }

    internal static Dictionary<string, ContentControl> GetOpenWindows(DockingManager dockSite, DocumentTabControl ChartTab)
    {
      var opened = new Dictionary<string, ContentControl>();
      foreach (var child in dockSite.Children)
      {
        if (child is ContentControl control)
        {
          opened[control.Name] = control;
        }
      }

      if (ChartTab != null && ChartTab.Container != null)
      {
        foreach (var child in ChartTab.Container.Items)
        {
          if (child is ContentControl control)
          {
            opened[control.Name] = control;
          }
        }
      }

      return opened;
    }

    internal static dynamic InsertNameIntoSortedList(string name, ObservableCollection<object> collection)
    {
      var entry = new ExpandoObject() as dynamic;
      entry.Name = name;

      int index = collection.ToList().BinarySearch(entry, TheSortableNameComparer);
      if (index < 0)
      {
        collection.Insert(~index, entry);
      }
      else
      {
        entry = collection[index];
      }

      return entry;
    }

    internal static void InitPetOwners(MainWindow main, SfDataGrid petMappingGrid, GridComboBoxColumn ownerList, ContentControl petMappingWindow)
    {
      // pet -> players
      petMappingGrid.ItemsSource = PetPlayersView;
      ownerList.ItemsSource = VerifiedPlayersView;
      PlayerManager.Instance.EventsNewPetMapping += (sender, mapping) =>
      {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
          var existing = PetPlayersView.FirstOrDefault(item => item.Pet.Equals(mapping.Pet, StringComparison.OrdinalIgnoreCase));
          if (existing != null)
          {
            if (existing.Owner != mapping.Owner)
            {
              PetPlayersView.Remove(existing);
              InsertPetMappingIntoSortedList(mapping, PetPlayersView);
            }
          }
          else
          {
            InsertPetMappingIntoSortedList(mapping, PetPlayersView);
          }

          DockingManager.SetHeader(petMappingWindow, "Pet Owners (" + PetPlayersView.Count + ")");
        });

        main.CheckComputeStats();
      };
    }

    internal static void InitVerifiedPlayers(MainWindow main, SfDataGrid playersGrid, GridComboBoxColumn classList,
      ContentControl playersWindow, ContentControl petMappingWindow)
    {
      // verified player table
      playersGrid.ItemsSource = VerifiedPlayersView;
      classList.ItemsSource = PlayerManager.Instance.GetClassList(true);
      PlayerManager.Instance.EventsNewVerifiedPlayer += (sender, name) =>
      {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
          var entry = InsertNameIntoSortedList(name, VerifiedPlayersView);
          entry.PlayerClass = PlayerManager.Instance.GetPlayerClass(name);
          DockingManager.SetHeader(playersWindow, string.Format(PLAYER_LIST_TITLE, VerifiedPlayersView.Count));
        });
      };

      PlayerManager.Instance.EventsUpdatePlayerClass += (name, playerClass) =>
      {
        var entry = new ExpandoObject() as dynamic;
        entry.Name = name;
        int index = VerifiedPlayersView.ToList().BinarySearch(entry, TheSortableNameComparer);
        if (index >= 0)
        {
          VerifiedPlayersView[index].PlayerClass = playerClass;
        }
      };

      PlayerManager.Instance.EventsRemoveVerifiedPlayer += (sender, name) =>
      {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
          var found = VerifiedPlayersView.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
          if (found != null)
          {
            VerifiedPlayersView.Remove(found);
            DockingManager.SetHeader(playersWindow, string.Format(PLAYER_LIST_TITLE, VerifiedPlayersView.Count));

            var existing = PetPlayersView.FirstOrDefault(item => item.Owner.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
              PetPlayersView.Remove(existing);
              DockingManager.SetHeader(petMappingWindow, "Pet Owners (" + PetPlayersView.Count + ")");
            }

            main.CheckComputeStats();
          }
        });
      };
    }

    internal static void InitVerifiedPets(MainWindow main, SfDataGrid petsGrid, ContentControl petsWindow, ContentControl petMappingWindow)
    {
      // verified pets table
      petsGrid.ItemsSource = VerifiedPetsView;
      PlayerManager.Instance.EventsNewVerifiedPet += (sender, name) => main.Dispatcher.InvokeAsync(() =>
      {
        InsertNameIntoSortedList(name, VerifiedPetsView);
        DockingManager.SetHeader(petsWindow, string.Format(PETS_LIST_TITLE, VerifiedPetsView.Count));
      });

      PlayerManager.Instance.EventsRemoveVerifiedPet += (sender, name) =>
      {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
          var found = VerifiedPetsView.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
          if (found != null)
          {
            VerifiedPetsView.Remove(found);
            DockingManager.SetHeader(petsWindow, string.Format(PETS_LIST_TITLE, VerifiedPetsView.Count));

            var existing = PetPlayersView.FirstOrDefault(item => item.Pet.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
              PetPlayersView.Remove(existing);
              DockingManager.SetHeader(petMappingWindow, "Pet Owners (" + PetPlayersView.Count + ")");
            }

            main.CheckComputeStats();
          }
        });
      };
    }

    internal static void ExportFights(List<Fight> fights)
    {
      var saveFileDialog = new SaveFileDialog();
      var fileName = "eqlog_" + ConfigUtil.PlayerName + "_" + ConfigUtil.ServerName + "-selected.txt";
      saveFileDialog.Filter = "Text Files (*.txt)|*.txt";
      saveFileDialog.FileName = string.Join("", fileName.Split(Path.GetInvalidFileNameChars()));

      if (saveFileDialog.ShowDialog().Value)
      {
        var dialog = new MessageWindow("Saving " + fights.Count + " Selected Fights.", EQLogParser.Resource.FILEMENU_SAVE_FIGHTS, false, true);

        Task.Delay(150).ContinueWith(task =>
        {
          bool accessError = false;

          try
          {
            using (var os = File.Open(saveFileDialog.FileName, FileMode.Create))
            {
              TimeRange range = new TimeRange();
              fights.ForEach(fight =>
              {
                range.Add(new TimeSegment(fight.BeginTime - 15, fight.LastTime));
              });

              if (range.TimeSegments.Count > 0)
              {
                using (var f = File.OpenRead(MainWindow.CurrentLogFile))
                {
                  StreamReader s = Helpers.GetStreamReader(f, range.TimeSegments[0].BeginTime);
                  while (!s.EndOfStream)
                  {
                    string line = s.ReadLine();
                    if (!string.IsNullOrEmpty(line) && line.Length > MainWindow.ACTION_INDEX)
                    {
                      string action = line.Substring(MainWindow.ACTION_INDEX);
                      if (ChatLineParser.ParseChatType(action) == null)
                      {
                        if (Helpers.TimeCheck(line, range.TimeSegments[0].BeginTime, range, out bool exceeds))
                        {
                          os.Write(Encoding.UTF8.GetBytes(line));
                          os.Write(Encoding.UTF8.GetBytes(Environment.NewLine));
                        }

                        if (exceeds)
                        {
                          break;
                        }
                      }
                    }
                  }
                }
              }
            }

            Application.Current.Dispatcher.InvokeAsync(() => dialog?.Close());
          }
          catch (IOException ex)
          {
            LOG.Error(ex);
            accessError = true;
          }
          catch (UnauthorizedAccessException uax)
          {
            LOG.Error(uax);
          }
          catch (SecurityException se)
          {
            LOG.Error(se);
          }
          catch (ArgumentNullException ane)
          {
            LOG.Error(ane);
          }
          finally
          {
            Application.Current.Dispatcher.InvokeAsync(() => dialog?.Close());

            if (accessError)
            {
              Application.Current.Dispatcher.InvokeAsync(() =>
              {
                new MessageWindow("Error Saving. Can not access export file.", EQLogParser.Resource.FILEMENU_SAVE_FIGHTS, false, false).Show();
              });
            }
          }
        });

        dialog.ShowDialog();
      }
    }

    internal static void ExportAsHTML(Dictionary<string, SummaryTable> tables)
    {
      try
      {
        var saveFileDialog = new SaveFileDialog();
        saveFileDialog.Filter = "HTML Files (*.html)|*.html";
        var fileName = DateUtil.GetCurrentDate("MM-dd-yy") + " " + tables.Values.First().GetTargetTitle();
        saveFileDialog.FileName = string.Join("", fileName.Split(Path.GetInvalidFileNameChars()));

        if (saveFileDialog.ShowDialog().Value)
        {
          TextFormatUtils.SaveHTML(saveFileDialog.FileName, tables);
        }
      }
      catch (IOException ex)
      {
        LOG.Error(ex);
      }
      catch (UnauthorizedAccessException uax)
      {
        LOG.Error(uax);
      }
      catch (SecurityException se)
      {
        LOG.Error(se);
      }
      catch (ArgumentNullException ane)
      {
        LOG.Error(ane);
      }
    }

    private static void InsertPetMappingIntoSortedList(PetMapping mapping, ObservableCollection<PetMapping> collection)
    {
      int index = collection.ToList().BinarySearch(mapping, TheSortablePetMappingComparer);
      if (index < 0)
      {
        collection.Insert(~index, mapping);
      }
      else
      {
        collection.Insert(index, mapping);
      }
    }

    private class SortablePetMappingComparer : IComparer<PetMapping>
    {
      public int Compare(PetMapping x, PetMapping y)
      {
        return string.CompareOrdinal(x?.Owner, y?.Owner);
      }
    }

    private class SortableNameComparer : IComparer<object>
    {
      public int Compare(object x, object y)
      {
        return string.CompareOrdinal(((dynamic)x)?.Name, ((dynamic)y)?.Name);
      }
    }
  }
}
