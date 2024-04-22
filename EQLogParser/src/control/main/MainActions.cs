﻿using FontAwesome5;
using log4net;
using Microsoft.Win32;
using Syncfusion.SfSkinManager;
using Syncfusion.Themes.MaterialDarkCustom.WPF;
using Syncfusion.Themes.MaterialLight.WPF;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.Windows.Tools.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EQLogParser
{
  static partial class MainActions
  {
    internal static event Action<string> EventsLogLoadingComplete;
    internal static event Action<string> EventsThemeChanged;
    internal static event Action<List<Fight>> EventsFightSelectionChanged;
    internal static event Action<string> EventsChartOpened;
    internal static event Action<PlayerStatsSelectionChangedEventArgs> EventsDamageSelectionChanged;
    internal static event Action<PlayerStatsSelectionChangedEventArgs> EventsHealingSelectionChanged;
    internal static event Action<PlayerStatsSelectionChangedEventArgs> EventsTankingSelectionChanged;
    internal static readonly HttpClient TheHttpClient = new();

    private const string PetsListTitle = "Verified Pets ({0})";
    private const string PlayerListTitle = "Verified Players ({0})";
    private static readonly ObservableCollection<dynamic> VerifiedPlayersView = [];
    private static readonly ObservableCollection<dynamic> VerifiedPetsView = [];
    private static readonly ObservableCollection<PetMapping> PetPlayersView = [];
    private static readonly SortablePetMappingComparer TheSortablePetMappingComparer = new();
    private static readonly SortableNameComparer TheSortableNameComparer = new();
    private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);

    internal static void FireChartOpened(string name) => EventsChartOpened?.Invoke(name);
    internal static void FireDamageSelectionChanged(PlayerStatsSelectionChangedEventArgs args) => EventsDamageSelectionChanged?.Invoke(args);
    internal static void FireTankingSelectionChanged(PlayerStatsSelectionChangedEventArgs args) => EventsTankingSelectionChanged?.Invoke(args);
    internal static void FireHealingSelectionChanged(PlayerStatsSelectionChangedEventArgs args) => EventsHealingSelectionChanged?.Invoke(args);
    internal static void FireLoadingEvent(string log) => EventsLogLoadingComplete?.Invoke(log);
    internal static void FireThemeChanged(string theme) => EventsThemeChanged?.Invoke(theme);
    internal static void FireFightSelectionChanged(List<Fight> fights) => EventsFightSelectionChanged?.Invoke(fights);

    internal static void AddDocumentWindows(DockingManager dockSite)
    {
      SyncFusionUtil.AddDocument(dockSite, typeof(TriggersTester), "triggerTestWindow", "Trigger Tester");
      SyncFusionUtil.AddDocument(dockSite, typeof(TriggersLogView), "triggerLogWindow", "Trigger Log");
      SyncFusionUtil.AddDocument(dockSite, typeof(QuickShareLogView), "quickShareLogWindow", "Quick Share Log");
      SyncFusionUtil.AddDocument(dockSite, typeof(HealingSummary), "healingSummaryWindow", "Healing Summary");
      SyncFusionUtil.AddDocument(dockSite, typeof(TankingSummary), "tankingSummaryWindow", "Tanking Summary");
      SyncFusionUtil.AddDocument(dockSite, typeof(DamageChart), "damageChartWindow", "DPS Chart");
      SyncFusionUtil.AddDocument(dockSite, typeof(HealingChart), "healingChartWindow", "Healing Chart");
      SyncFusionUtil.AddDocument(dockSite, typeof(TankingChart), "tankingChartWindow", "Tanking Chart");
      SyncFusionUtil.AddDocument(dockSite, typeof(ChatViewer), "chatWindow", "Chat Archive");
      SyncFusionUtil.AddDocument(dockSite, typeof(EventViewer), "specialEventsWindow", "Misc Events");
      SyncFusionUtil.AddDocument(dockSite, typeof(RandomViewer), "randomsWindow", "Random Rolls");
      SyncFusionUtil.AddDocument(dockSite, typeof(LootViewer), "lootWindow", "Looted Items");
      SyncFusionUtil.AddDocument(dockSite, typeof(TriggersView), "triggersWindow", "Trigger Manager");
      SyncFusionUtil.AddDocument(dockSite, typeof(NpcStatsViewer), "spellResistsWindow", "Spell Resists");
      SyncFusionUtil.AddDocument(dockSite, typeof(SpellDamageStatsViewer), "spellDamageStatsWindow", "Spell Damage");
      SyncFusionUtil.AddDocument(dockSite, typeof(TauntStatsViewer), "tauntStatsWindow", "Taunt Usage");
      SyncFusionUtil.AddDocument(dockSite, typeof(DamageSummary), "damageSummaryWindow", "DPS Summary", true);
    }

    internal static TimeRange GetAllRanges()
    {
      TimeRange result = null;
      UiUtil.InvokeNow(() =>
      {
        result = (Application.Current.MainWindow as MainWindow)?.GetFightTable()?.GetAllRanges();
      });

      return result ?? new TimeRange();
    }

    internal static List<Fight> GetFights()
    {
      List<Fight> result = null;
      UiUtil.InvokeNow(() =>
      {
        result = (Application.Current.MainWindow as MainWindow)?.GetFightTable()?.GetFights();
      });

      return result;
    }

    internal static List<Fight> GetSelectedFights()
    {
      List<Fight> result = null;
      UiUtil.InvokeNow(() =>
      {
        result = (Application.Current.MainWindow as MainWindow)?.GetFightTable()?.GetSelectedFights();
      });

      return result;
    }

    internal static void CheckVersion(TextBlock errorText)
    {
      var version = Application.ResourceAssembly.GetName().Version;
      Task.Delay(2000).ContinueWith(_ =>
      {
        try
        {
          var request = TheHttpClient.GetStringAsync("https://github.com/kauffman12/EQLogParser/blob/master/README.md");
          request.Wait();

          var matches = InstallerName().Match(request.Result);
          if (version != null && matches.Success && matches.Groups.Count == 5 && int.TryParse(matches.Groups[2].Value, out var v1) &&
              int.TryParse(matches.Groups[3].Value, out var v2) && int.TryParse(matches.Groups[4].Value, out var v3)
              && (v1 > version.Major || (v1 == version.Major && v2 > version.Minor) ||
                  (v1 == version.Major && v2 == version.Minor && v3 > version.Build)))
          {
            async void Action()
            {
              var msg = new MessageWindow($"Version {matches.Groups[1].Value} is Available. Download and Install?", Resource.CHECK_VERSION,
                MessageWindow.IconType.Question, "Yes");
              msg.ShowDialog();

              if (msg.IsYes1Clicked)
              {
                var url = "https://github.com/kauffman12/EQLogParser/raw/master/Release/EQLogParser-install-" + matches.Groups[1].Value + ".exe";

                try
                {
                  await using var download = await TheHttpClient.GetStreamAsync(url);
                  var path = Environment.ExpandEnvironmentVariables("%userprofile%\\Downloads");
                  if (!Directory.Exists(path))
                  {
                    new MessageWindow("Unable to Access Downloads Folder. Can Not Download Update.", Resource.CHECK_VERSION).ShowDialog();
                    return;
                  }

                  path += "\\AutoUpdateEQLogParser";
                  if (!Directory.Exists(path))
                  {
                    Directory.CreateDirectory(path);
                  }

                  var fullPath = $"{path}\\EQLogParser-install-{matches.Groups[1].Value}.exe";
                  await using (var fs = new FileStream(fullPath, FileMode.Create))
                  {
                    await download.CopyToAsync(fs);
                  }

                  if (File.Exists(fullPath))
                  {
                    var process = Process.Start(fullPath);
                    if (process is { HasExited: false })
                    {
                      await Task.Delay(1000).ContinueWith(_ => { UiUtil.InvokeAsync(() => Application.Current.MainWindow?.Close()); });
                    }
                  }
                }
                catch (Exception ex2)
                {
                  new MessageWindow("Problem Installing Updates. Check Error Log for Details.", Resource.CHECK_VERSION).ShowDialog();
                  Log.Error("Error Installing Updates", ex2);
                }
              }
            }

            UiUtil.InvokeAsync(Action);
          }
        }
        catch (Exception ex)
        {
          Log.Error("Error Checking for Updates", ex);
          UiUtil.InvokeAsync(() => errorText.Text = "Update Check Failed. Firewall?");
        }
      });
    }

    internal static void Cleanup()
    {
      try
      {
        var path = Environment.ExpandEnvironmentVariables("%userprofile%\\Downloads");
        if (!Directory.Exists(path))
        {
          return;
        }

        path += "\\AutoUpdateEQLogParser";
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
      catch (Exception e)
      {
        Log.Error(e);
      }
    }

    internal static void CreateFontFamiliesMenuItems(MenuItem parent, RoutedEventHandler callback, string currentFamily)
    {
      foreach (var family in UiElementUtil.GetCommonFontFamilyNames())
      {
        parent.Items.Add(CreateMenuItem(family, callback, EFontAwesomeIcon.Solid_Check));
      }

      return;

      MenuItem CreateMenuItem(string name, RoutedEventHandler handler, EFontAwesomeIcon awesome)
      {
        var imageAwesome = new ImageAwesome
        {
          Icon = awesome,
          Style = (Style)Application.Current.Resources["EQIconStyle"],
          Visibility = (name == currentFamily) ? Visibility.Visible : Visibility.Hidden
        };

        var menuItem = new MenuItem { Header = name };
        menuItem.Click += handler;
        menuItem.Icon = imageAwesome;
        return menuItem;
      }
    }

    internal static void CreateFontSizesMenuItems(MenuItem parent, RoutedEventHandler callback, double currentSize)
    {
      parent.Items.Add(CreateMenuItem(10, callback, EFontAwesomeIcon.Solid_Check));
      parent.Items.Add(CreateMenuItem(11, callback, EFontAwesomeIcon.Solid_Check));
      parent.Items.Add(CreateMenuItem(12, callback, EFontAwesomeIcon.Solid_Check));
      parent.Items.Add(CreateMenuItem(13, callback, EFontAwesomeIcon.Solid_Check));
      parent.Items.Add(CreateMenuItem(14, callback, EFontAwesomeIcon.Solid_Check));
      return;

      MenuItem CreateMenuItem(double size, RoutedEventHandler handler, EFontAwesomeIcon awesome)
      {
        var imageAwesome = new ImageAwesome
        {
          Icon = awesome,
          Style = (Style)Application.Current.Resources["EQIconStyle"],
          Visibility = size.Equals(currentSize) ? Visibility.Visible : Visibility.Hidden
        };

        var menuItem = new MenuItem { Header = size + "pt", Tag = size };
        menuItem.Click += handler;
        menuItem.Icon = imageAwesome;
        return menuItem;
      }
    }

    internal static void CreateOpenLogMenuItems(MenuItem parent, RoutedEventHandler callback)
    {
      parent.Items.Add(CreateMenuItem("Now", "0", callback, EFontAwesomeIcon.Solid_CalendarDay));
      parent.Items.Add(CreateMenuItem("Last Hour", "1", callback, EFontAwesomeIcon.Solid_CalendarDay));
      parent.Items.Add(CreateMenuItem("Last  8 Hours", "8", callback, EFontAwesomeIcon.Solid_CalendarDay));
      parent.Items.Add(CreateMenuItem("Last 24 Hours", "24", callback, EFontAwesomeIcon.Solid_CalendarDay));
      parent.Items.Add(CreateMenuItem("Last 2 Days", "48", callback, EFontAwesomeIcon.Solid_CalendarAlt));
      parent.Items.Add(CreateMenuItem("Last  7 Days", "168", callback, EFontAwesomeIcon.Solid_CalendarAlt));
      parent.Items.Add(CreateMenuItem("Last 14 Days", "336", callback, EFontAwesomeIcon.Solid_CalendarAlt));
      parent.Items.Add(CreateMenuItem("Last 30 Days", "720", callback, EFontAwesomeIcon.Solid_CalendarAlt));
      parent.Items.Add(CreateMenuItem("Everything", null, callback, EFontAwesomeIcon.Solid_Infinity));
      return;

      static MenuItem CreateMenuItem(string name, string value, RoutedEventHandler handler, EFontAwesomeIcon awesome)
      {
        var imageAwesome = new ImageAwesome { Icon = awesome, Style = (Style)Application.Current.Resources["EQIconStyle"] };
        var menuItem = new MenuItem { Header = name, Tag = value };
        menuItem.Click += handler;
        menuItem.Icon = imageAwesome;
        return menuItem;
      }
    }

    internal static void UpdateCheckedMenuItem(MenuItem selectedItem, ItemCollection items)
    {
      foreach (var item in items)
      {
        if (item is MenuItem { Icon: ImageAwesome image } menuItem)
        {
          image.Visibility = (menuItem == selectedItem) ? Visibility.Visible : Visibility.Hidden;
        }
      }
    }

    internal static void SetTheme(Window window, string theme)
    {
      if (window != null)
      {
        switch (theme)
        {
          case "MaterialLight":
            SfSkinManager.SetTheme(window, new Theme("MaterialLight"));
            break;
          default:
            SfSkinManager.SetTheme(window, new Theme("MaterialDarkCustom;MaterialDark"));
            break;
        }
      }
    }

    internal static void LoadTheme(MainWindow main, string theme)
    {
      Application.Current.Resources["EQTitleSize"] = MainWindow.CurrentFontSize + 2;
      Application.Current.Resources["EQContentSize"] = MainWindow.CurrentFontSize;
      Application.Current.Resources["EQDescriptionSize"] = MainWindow.CurrentFontSize - 1;
      Application.Current.Resources["EQButtonHeight"] = MainWindow.CurrentFontSize + 12 + (MainWindow.CurrentFontSize % 2 == 0 ? 1 : 0);
      Application.Current.Resources["EQTableHeaderRowHeight"] = MainWindow.CurrentFontSize + 14;
      Application.Current.Resources["EQTableRowHeight"] = MainWindow.CurrentFontSize + 12;

      if (theme == "MaterialLight")
      {
        var themeSettings = new MaterialLightThemeSettings
        {
          PrimaryBackground = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF343434")! },
          FontFamily = new FontFamily(MainWindow.CurrentFontFamily),
          BodyAltFontSize = MainWindow.CurrentFontSize - 2,
          BodyFontSize = MainWindow.CurrentFontSize,
          HeaderFontSize = MainWindow.CurrentFontSize + 4,
          SubHeaderFontSize = MainWindow.CurrentFontSize + 2,
          SubTitleFontSize = MainWindow.CurrentFontSize,
          TitleFontSize = MainWindow.CurrentFontSize + 2
        };
        SfSkinManager.RegisterThemeSettings("MaterialLight", themeSettings);
        Application.Current.Resources["EQGoodForegroundBrush"] = new SolidColorBrush { Color = Colors.DarkGreen };
        Application.Current.Resources["EQMenuIconBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF3d7baf")! };
        Application.Current.Resources["EQSearchBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FFa7baab")! };
        Application.Current.Resources["EQWarnBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FFeaa6ac")! };
        Application.Current.Resources["EQWarnForegroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FFb02021")! };
        Application.Current.Resources["EQStopForegroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FFcc434d")! };
        Application.Current.Resources["EQDisabledBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#88000000")! };
        SfSkinManager.SetTheme(main, new Theme("MaterialLight"));
        LoadDictionary("/Syncfusion.Themes.MaterialLight.WPF;component/MSControl/CheckBox.xaml");
        LoadDictionary("/Syncfusion.Themes.MaterialLight.WPF;component/SfDataGrid/SfDataGrid.xaml");
        LoadDictionary("/Syncfusion.Themes.MaterialLight.WPF;component/Common/Brushes.xaml");
        main.BorderBrush = Application.Current.Resources["ContentBackgroundAlt2"] as SolidColorBrush;

        if (!string.IsNullOrEmpty(main.statusText?.Text))
        {
          main.statusText.Foreground = Application.Current.Resources["EQGoodForegroundBrush"] as SolidColorBrush;
        }
      }
      else
      {
        var themeSettings = new MaterialDarkCustomThemeSettings
        {
          PrimaryBackground = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FFE1E1E1")! },
          FontFamily = new FontFamily(MainWindow.CurrentFontFamily),
          BodyAltFontSize = MainWindow.CurrentFontSize - 2,
          BodyFontSize = MainWindow.CurrentFontSize,
          HeaderFontSize = MainWindow.CurrentFontSize + 4,
          SubHeaderFontSize = MainWindow.CurrentFontSize + 2,
          SubTitleFontSize = MainWindow.CurrentFontSize,
          TitleFontSize = MainWindow.CurrentFontSize + 2
        };
        SfSkinManager.RegisterThemeSettings("MaterialDarkCustom", themeSettings);

        Application.Current.Resources["EQGoodForegroundBrush"] = new SolidColorBrush { Color = Colors.LightGreen };
        Application.Current.Resources["EQMenuIconBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF4F9FE2")! };
        Application.Current.Resources["EQSearchBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF314435")! };
        Application.Current.Resources["EQWarnBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF96410d")! };
        Application.Current.Resources["EQWarnForegroundBrush"] = new SolidColorBrush { Color = Colors.Orange };
        Application.Current.Resources["EQStopForegroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FFcc434d")! };
        Application.Current.Resources["EQDisabledBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#88FFFFFF")! };
        SfSkinManager.SetTheme(main, new Theme("MaterialDarkCustom"));
        LoadDictionary("/Syncfusion.Themes.MaterialDarkCustom.WPF;component/MSControl/CheckBox.xaml");
        LoadDictionary("/Syncfusion.Themes.MaterialDarkCustom.WPF;component/SfDataGrid/SfDataGrid.xaml");
        LoadDictionary("/Syncfusion.Themes.MaterialDarkCustom.WPF;component/Common/Brushes.xaml");
        main.BorderBrush = Application.Current.Resources["ContentBackgroundAlt2"] as SolidColorBrush;

        if (!string.IsNullOrEmpty(main.statusText?.Text))
        {
          main.statusText.Foreground = Application.Current.Resources["EQGoodForegroundBrush"] as SolidColorBrush;
        }
      }

      Application.Current.Resources["PreviewBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#BB000000")! };
      Application.Current.Resources["DamageOverlayBackgroundBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#99000000")! };
      Application.Current.Resources["DamageOverlayDamageBrush"] = new SolidColorBrush { Color = Colors.White };
      Application.Current.Resources["DamageOverlayProgressBrush"] = new SolidColorBrush { Color = (Color)ColorConverter.ConvertFromString("#FF1D397E")! };
      FireThemeChanged(theme);
    }

    // should already run on the UI thread
    internal static void Clear(ContentControl petsWindow, ContentControl playersWindow)
    {
      PetPlayersView.Clear();
      VerifiedPetsView.Clear();
      VerifiedPlayersView.Clear();

      var entry = new ExpandoObject() as dynamic;
      entry.Name = Labels.Unassigned;
      VerifiedPlayersView.Add(entry);
      DockingManager.SetHeader(petsWindow, string.Format(PetsListTitle, VerifiedPetsView.Count));
      DockingManager.SetHeader(playersWindow, string.Format(PlayerListTitle, VerifiedPlayersView.Count));
    }

    internal static dynamic InsertNameIntoSortedList(string name, ObservableCollection<object> collection)
    {
      var entry = new ExpandoObject() as dynamic;
      entry.Name = name;

      var index = collection.ToList().BinarySearch(entry, TheSortableNameComparer);
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

    // should already run on the UI thread
    internal static void InitPetOwners(MainWindow main, SfDataGrid petMappingGrid, GridComboBoxColumn ownerList, ContentControl petMappingWindow)
    {
      // pet -> players
      petMappingGrid.ItemsSource = PetPlayersView;
      ownerList.ItemsSource = VerifiedPlayersView;
      PlayerManager.Instance.EventsNewPetMapping += (_, mapping) =>
      {
        UiUtil.InvokeAsync(() =>
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

    // should already run on the UI thread
    internal static void InitVerifiedPlayers(MainWindow main, SfDataGrid playersGrid, GridComboBoxColumn classList,
      ContentControl playersWindow, ContentControl petMappingWindow)
    {
      // verified player table
      playersGrid.ItemsSource = VerifiedPlayersView;
      classList.ItemsSource = PlayerManager.Instance.GetClassList(true);
      PlayerManager.Instance.EventsNewVerifiedPlayer += (_, name) =>
      {
        UiUtil.InvokeAsync(() =>
        {
          var entry = InsertNameIntoSortedList(name, VerifiedPlayersView);
          entry.PlayerClass = PlayerManager.Instance.GetPlayerClass(name);
          DockingManager.SetHeader(playersWindow, string.Format(PlayerListTitle, VerifiedPlayersView.Count));
        });
      };

      PlayerManager.Instance.EventsUpdatePlayerClass += (name, playerClass) =>
      {
        UiUtil.InvokeAsync(() =>
        {
          var entry = new ExpandoObject() as dynamic;
          entry.Name = name;
          int index = VerifiedPlayersView.ToList().BinarySearch(entry, TheSortableNameComparer);
          if (index >= 0)
          {
            VerifiedPlayersView[index].PlayerClass = playerClass;
          }
        });
      };

      PlayerManager.Instance.EventsRemoveVerifiedPlayer += (_, name) =>
      {
        UiUtil.InvokeAsync(() =>
        {
          var found = VerifiedPlayersView.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
          if (found != null)
          {
            VerifiedPlayersView.Remove(found);
            DockingManager.SetHeader(playersWindow, string.Format(PlayerListTitle, VerifiedPlayersView.Count));

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
      PlayerManager.Instance.EventsNewVerifiedPet += (_, name) => main.Dispatcher.InvokeAsync(() =>
      {
        InsertNameIntoSortedList(name, VerifiedPetsView);
        DockingManager.SetHeader(petsWindow, string.Format(PetsListTitle, VerifiedPetsView.Count));
      });

      PlayerManager.Instance.EventsRemoveVerifiedPet += (_, name) =>
      {
        UiUtil.InvokeAsync(() =>
        {
          var found = VerifiedPetsView.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
          if (found != null)
          {
            VerifiedPetsView.Remove(found);
            DockingManager.SetHeader(petsWindow, string.Format(PetsListTitle, VerifiedPetsView.Count));

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

    internal static void UpdateDamageOption(UIElement icon, bool enabled, string option)
    {
      ConfigUtil.SetSetting(option, enabled);
      icon.Visibility = enabled ? Visibility.Visible : Visibility.Hidden;
      var options = new GenerateStatsOptions();
      Task.Run(() => DamageStatsManager.Instance.RebuildTotalStats(options));
    }

    internal static void ExportFights(string currentFile, List<Fight> fights)
    {
      var saveFileDialog = new SaveFileDialog();
      var fileName = $"eqlog_{ConfigUtil.PlayerName}_{ConfigUtil.ServerName}-selected.txt";
      saveFileDialog.Filter = "Text Files (*.txt)|*.txt";
      saveFileDialog.FileName = string.Join("", fileName.Split(Path.GetInvalidFileNameChars()));

      if (saveFileDialog.ShowDialog() == true)
      {
        var dialog = new MessageWindow($"Saving {fights.Count} Selected Fights.", Resource.FILEMENU_SAVE_FIGHTS,
          MessageWindow.IconType.Save);

        Task.Delay(150).ContinueWith(_ =>
        {
          var accessError = false;

          try
          {
            using (var os = File.Open(saveFileDialog.FileName, FileMode.Create))
            {
              var range = new TimeRange();
              fights.ForEach(fight =>
              {
                range.Add(new TimeSegment(fight.BeginTime - 15, fight.LastTime));
              });

              if (range.TimeSegments.Count > 0)
              {
                using var f = File.OpenRead(currentFile);
                var s = FileUtil.GetStreamReader(f, range.TimeSegments[0].BeginTime);
                while (!s.EndOfStream)
                {
                  var line = s.ReadLine();
                  if (string.IsNullOrEmpty(line) || line.Length <= MainWindow.ActionIndex)
                  {
                    continue;
                  }

                  var action = line[MainWindow.ActionIndex..];
                  if (ChatLineParser.ParseChatType(action) != null)
                  {
                    continue;
                  }

                  if (TimeRange.TimeCheck(line, range.TimeSegments[0].BeginTime, range, out var exceeds))
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

            UiUtil.InvokeNow(() => dialog.Close());
          }
          catch (IOException ex)
          {
            Log.Error(ex);
            accessError = true;
          }
          catch (UnauthorizedAccessException uax)
          {
            Log.Error(uax);
          }
          catch (SecurityException se)
          {
            Log.Error(se);
          }
          catch (ArgumentNullException ane)
          {
            Log.Error(ane);
          }
          finally
          {
            UiUtil.InvokeAsync(() =>
            {
              dialog.Close();

              if (accessError)
              {
                new MessageWindow("Error Saving. Can not access save file.", Resource.FILEMENU_SAVE_FIGHTS, MessageWindow.IconType.Save).ShowDialog();
              }
            });
          }
        });

        dialog.ShowDialog();
      }
    }

    internal static void ExportAsHtml(Dictionary<string, SummaryTable> tables)
    {
      try
      {
        var saveFileDialog = new SaveFileDialog
        {
          Filter = "HTML Files (*.html)|*.html"
        };

        var fileName = DateUtil.GetCurrentDate("MM-dd-yy") + " ";
        if (tables.Values.FirstOrDefault() is { } summary)
        {
          fileName += summary.GetTargetTitle();
        }
        else
        {
          fileName += "No Summaries Exported";
        }

        saveFileDialog.FileName = string.Join("", fileName.Split(Path.GetInvalidFileNameChars()));

        if (saveFileDialog.ShowDialog() == true)
        {
          TextUtils.SaveHtml(saveFileDialog.FileName, tables);
        }
      }
      catch (IOException ex)
      {
        Log.Error(ex);
      }
      catch (UnauthorizedAccessException uax)
      {
        Log.Error(uax);
      }
      catch (SecurityException se)
      {
        Log.Error(se);
      }
      catch (ArgumentNullException ane)
      {
        Log.Error(ane);
      }
    }

    internal static void OpenFileWithDefault(string fileName)
    {
      try
      {
        Process.Start(new ProcessStartInfo { FileName = fileName, UseShellExecute = true });
      }
      catch (Exception ex)
      {
        Log.Error(ex);
      }
    }

    private static void LoadDictionary(string path)
    {
      var dict = new ResourceDictionary
      {
        Source = new Uri(path, UriKind.RelativeOrAbsolute)
      };

      foreach (var key in dict.Keys)
      {
        Application.Current.Resources[key] = dict[key];
      }
    }

    private static void InsertPetMappingIntoSortedList(PetMapping mapping, ObservableCollection<PetMapping> collection)
    {
      var index = collection.ToList().BinarySearch(mapping, TheSortablePetMappingComparer);
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

    [GeneratedRegex(@"EQLogParser-install-((\d)\.(\d)\.(\d?\d?\d))\.exe")]
    private static partial Regex InstallerName();
  }
}
