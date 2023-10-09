﻿using Syncfusion.Data.Extensions;
using Syncfusion.Windows.PropertyGrid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EQLogParser
{
  /// <summary>
  /// Interaction logic for TriggersView.xaml
  /// </summary>
  public partial class TriggersView : UserControl, IDisposable
  {
    private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private const string LABEL_NEW_TEXT_OVERLAY = "New Text Overlay";
    private const string LABEL_NEW_TIMER_OVERLAY = "New Timer Overlay";
    private const string LABEL_NEW_TRIGGER = "New Trigger";
    private const string LABEL_NEW_FOLDER = "New Folder";
    private readonly Dictionary<string, Window> PreviewWindows = new Dictionary<string, Window>();
    private TriggerConfig TheConfig;
    private FileSystemWatcher Watcher;
    private PatternEditor PatternEditor;
    private PatternEditor EndEarlyPatternEditor;
    private PatternEditor EndEarlyPattern2Editor;
    private RangeEditor TopEditor;
    private RangeEditor LeftEditor;
    private RangeEditor HeightEditor;
    private RangeEditor WidthEditor;
    private SpeechSynthesizer TestSynth = null;
    private string CurrentCharacterId = null;
    private GridLength CharacterViewWidth;
    private bool Ready = false;

    public TriggersView()
    {
      InitializeComponent();

      CharacterViewWidth = mainGrid.ColumnDefinitions[0].Width;
      TheConfig = TriggerStateManager.Instance.GetConfig();
      characterView.SetConfig(TheConfig);
      UpdateConfig(TheConfig);

      if ((TestSynth = TriggerUtil.GetSpeechSynthesizer()) != null)
      {
        voices.ItemsSource = TestSynth.GetInstalledVoices().Select(voice => voice.VoiceInfo.Name).ToList();
      }

      if (ConfigUtil.IfSetOrElse("TriggersWatchForGINA", false))
      {
        watchGina.IsChecked = true;
      }

      var selectedVoice = TriggerUtil.GetSelectedVoice();
      if (voices.ItemsSource is List<string> populated && populated.IndexOf(selectedVoice) is int found && found > -1)
      {
        voices.SelectedIndex = found;
      }

      rateOption.SelectedIndex = TriggerUtil.GetVoiceRate();
      var fileList = new ObservableCollection<string>();
      Watcher = TriggerUtil.CreateSoundsWatcher(fileList);

      TopEditor = (RangeEditor)AddEditorInstance(new RangeEditor(typeof(long), 0, 9999), "Top");
      HeightEditor = (RangeEditor)AddEditorInstance(new RangeEditor(typeof(long), 0, 9999), "Height");
      LeftEditor = (RangeEditor)AddEditorInstance(new RangeEditor(typeof(long), 0, 9999), "Left");
      WidthEditor = (RangeEditor)AddEditorInstance(new RangeEditor(typeof(long), 0, 9999), "Width");
      PatternEditor = (PatternEditor)AddEditorInstance(new PatternEditor(), "Pattern");
      EndEarlyPatternEditor = (PatternEditor)AddEditorInstance(new PatternEditor(), "EndEarlyPattern");
      EndEarlyPattern2Editor = (PatternEditor)AddEditorInstance(new PatternEditor(), "EndEarlyPattern2");
      AddEditor<CheckComboBoxEditor>("SelectedTextOverlays", "SelectedTimerOverlays");
      AddEditor<ColorEditor>("OverlayBrush", "FontBrush", "ActiveBrush", "IdleBrush", "ResetBrush", "BackgroundBrush");
      AddEditor<DurationEditor>("ResetDurationTimeSpan", "IdleTimeoutTimeSpan");
      AddEditor<ExampleTimerBar>("TimerBarPreview");
      AddEditor<OptionalColorEditor>("TriggerActiveBrush", "TriggerFontBrush");
      AddEditor<TriggerListsEditor>("TriggerAgainOption", "FontSize", "FontFamily", "SortBy", "TimerMode", "TimerType");
      AddEditor<WrapTextEditor>("EndEarlyTextToDisplay", "EndTextToDisplay", "TextToDisplay", "WarningTextToDisplay", "Comments", "OverlayComments");
      AddEditorInstance(new RangeEditor(typeof(double), 0.2, 2.0), "DurationSeconds");
      AddEditorInstance(new TextSoundEditor(fileList), "SoundOrText");
      AddEditorInstance(new TextSoundEditor(fileList), "EndEarlySoundOrText");
      AddEditorInstance(new TextSoundEditor(fileList), "EndSoundOrText");
      AddEditorInstance(new TextSoundEditor(fileList), "WarningSoundOrText");
      AddEditorInstance(new RangeEditor(typeof(long), 1, 5), "Priority");
      AddEditorInstance(new RangeEditor(typeof(long), 0, 99999), "WarningSeconds");
      AddEditorInstance(new RangeEditor(typeof(double), 0, 99999), "RepeatedResetTime");
      AddEditorInstance(new DurationEditor(2), "DurationTimeSpan");
      AddEditorInstance(new RangeEditor(typeof(long), 1, 60), "FadeDelay");

      void AddEditor<T>(params string[] propNames) where T : new()
      {
        foreach (var name in propNames)
        {
          var editor = new CustomEditor { Editor = (ITypeEditor)new T() };
          editor.Properties.Add(name);
          thePropertyGrid.CustomEditorCollection.Add(editor);
        }
      }

      ITypeEditor AddEditorInstance(ITypeEditor typeEditor, string propName)
      {
        var editor = new CustomEditor { Editor = typeEditor };
        editor.Properties.Add(propName);
        thePropertyGrid.CustomEditorCollection.Add(editor);
        return editor.Editor;
      }

      theTreeView.Init(CurrentCharacterId, IsCancelSelection, !TheConfig.IsAdvanced);
      theTreeView.TreeSelectionChangedEvent += TreeSelectionChangedEvent;
      theTreeView.ClosePreviewOverlaysEvent += ClosePreviewOverlaysEvent;
      TriggerStateManager.Instance.DeleteEvent += TriggerOverlayDeleteEvent;
      TriggerStateManager.Instance.TriggerUpdateEvent += TriggerUpdateEvent;
      TriggerStateManager.Instance.TriggerConfigUpdateEvent += TriggerConfigUpdateEvent;
      characterView.SelectedCharacterEvent += CharacterSelectedCharacterEvent;
      Ready = true;
    }

    internal bool IsCancelSelection()
    {
      dynamic model = thePropertyGrid?.SelectedObject;
      var cancel = false;
      if (saveButton.IsEnabled)
      {
        if (model is TriggerPropertyModel || model is TextOverlayPropertyModel || model is TimerOverlayPropertyModel)
        {
          if (model?.Node?.Name is string name)
          {
            var msgDialog = new MessageWindow("Do you want to save changes to " + name + "?", EQLogParser.Resource.UNSAVED,
              MessageWindow.IconType.Question, "Don't Save", "Save");
            msgDialog.ShowDialog();
            cancel = !msgDialog.IsYes1Clicked && !msgDialog.IsYes2Clicked;
            if (msgDialog.IsYes2Clicked)
            {
              SaveClick(this, null);
            }
          }
        }
      }

      return cancel;
    }

    private void TriggerConfigUpdateEvent(TriggerConfig config) => UpdateConfig(config);

    private void BasicChecked(object sender, RoutedEventArgs e)
    {
      if (Ready && sender is CheckBox checkBox)
      {
        TheConfig.IsEnabled = checkBox?.IsChecked == true;
        TriggerStateManager.Instance.UpdateConfig(TheConfig);
      }
    }

    private void CharacterSelectedCharacterEvent(TriggerCharacter character)
    {
      if (character == null)
      {
        if (CurrentCharacterId != null)
        {
          CurrentCharacterId = null;
          thePropertyGrid.SelectedObject = null;
          theTreeView.EnableAndRefreshTriggers(false, CurrentCharacterId);
        }
      }
      else
      {
        if (CurrentCharacterId != character.Id)
        {
          CurrentCharacterId = character.Id;
          thePropertyGrid.SelectedObject = null;
          theTreeView.EnableAndRefreshTriggers(true, CurrentCharacterId);
        }
      }
    }

    private void ToggleAdvancedPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
      if (advancedText != null)
      {
        if (advancedText.Text == "Switch to Advanced Settings")
        {
          TheConfig.IsAdvanced = true;
          basicCheckBox.Visibility = Visibility.Collapsed;
        }
        else
        {
          TheConfig.IsAdvanced = false;
          basicCheckBox.Visibility = Visibility.Visible;
        }

        TriggerStateManager.Instance.UpdateConfig(TheConfig);
      }
    }

    private void UpdateConfig(TriggerConfig config)
    {
      TheConfig = config;
      basicCheckBox.Visibility = !TheConfig.IsAdvanced ? Visibility.Visible : Visibility.Collapsed;
      basicCheckBox.IsChecked = TheConfig.IsEnabled;

      if (TheConfig.IsAdvanced)
      {
        CharacterSelectedCharacterEvent(characterView.GetSelectedCharacter());

        if (TheConfig.Characters.Count(user => user.IsEnabled) is int count && count > 0)
        {
          titleLabel.SetResourceReference(Label.ForegroundProperty, "EQGoodForegroundBrush");
          var updatedTitle = $"Triggers Active for {count} Character";
          if (count > 1)
          {
            updatedTitle = $"{updatedTitle}s";
          }
          titleLabel.Content = updatedTitle;
        }
        else
        {
          titleLabel.SetResourceReference(Label.ForegroundProperty, "EQStopForegroundBrush");
          titleLabel.Content = "No Triggers Active";
        }

        advancedText.Text = "Switch to Basic Settings";
        mainGrid.ColumnDefinitions[0].Width = CharacterViewWidth;
        mainGrid.ColumnDefinitions[1].Width = new GridLength(2);
      }
      else
      {
        if (CurrentCharacterId != TriggerStateManager.DEFAULT_USER)
        {
          CurrentCharacterId = TriggerStateManager.DEFAULT_USER;
          thePropertyGrid.SelectedObject = null;
          theTreeView.EnableAndRefreshTriggers(true, CurrentCharacterId);
        }

        if (TheConfig.IsEnabled)
        {
          titleLabel.SetResourceReference(Label.ForegroundProperty, "EQGoodForegroundBrush");
          titleLabel.Content = "Triggers Active";
        }
        else
        {
          titleLabel.SetResourceReference(Label.ForegroundProperty, "EQStopForegroundBrush");
          titleLabel.Content = "Check to Activate Triggers";
        }

        advancedText.Text = "Switch to Advanced Settings";
        mainGrid.ColumnDefinitions[0].Width = new GridLength(0);
        mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
      }

      advancedText.UpdateLayout();
      advancedText.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
      advancedText.Arrange(new Rect(advancedText.DesiredSize));
      underlineRect.Width = advancedText.ActualWidth;
    }

    private void ClosePreviewOverlaysEvent(bool _)
    {
      PreviewWindows.Values.ToList().ForEach(window => window.Close());
      PreviewWindows.Clear();
    }

    private void OptionsChanged(object sender, RoutedEventArgs e)
    {
      if (Ready)
      {
        if (sender == watchGina)
        {
          ConfigUtil.SetSetting("TriggersWatchForGINA", watchGina.IsChecked.Value.ToString(CultureInfo.CurrentCulture));
        }
        else if (sender == voices)
        {
          if (voices.SelectedValue is string voiceName)
          {
            ConfigUtil.SetSetting("TriggersSelectedVoice", voiceName);
            TriggerManager.Instance.SetVoice(voiceName);

            if (TestSynth != null)
            {
              TestSynth.Rate = TriggerUtil.GetVoiceRate();
              TestSynth.SelectVoice(voiceName);
              TestSynth.SpeakAsync(voiceName);
            }
          }
        }
        else if (sender == rateOption)
        {
          ConfigUtil.SetSetting("TriggersVoiceRate", rateOption.SelectedIndex.ToString(CultureInfo.CurrentCulture));
          TriggerManager.Instance.SetVoiceRate(rateOption.SelectedIndex);

          if (TestSynth != null)
          {
            TestSynth.Rate = rateOption.SelectedIndex;
            if (TriggerUtil.GetSelectedVoice() is string voice && !string.IsNullOrEmpty(voice))
            {
              TestSynth.SelectVoice(voice);
            }
            var rateText = rateOption.SelectedIndex == 0 ? "Default Voice Rate" : "Voice Rate " + rateOption.SelectedIndex.ToString();
            TestSynth.SpeakAsync(rateText);
          }
        }
      }
    }

    private void TriggerOverlayDeleteEvent(string id)
    {
      if (PreviewWindows.Remove(id, out var window))
      {
        window?.Close();
      }

      thePropertyGrid.SelectedObject = null;
      thePropertyGrid.IsEnabled = false;
    }

    private void EnableCategories(bool trigger, bool basicTimer, bool shortTimer, bool overlay, bool overlayTimer,
      bool overlayAssigned, bool overlayText, bool cooldownTimer)
    {
      PropertyGridUtil.EnableCategories(thePropertyGrid, new[]
      {
        new { Name = patternItem.CategoryName, IsEnabled = trigger },
        new { Name = timerDurationItem.CategoryName, IsEnabled = basicTimer },
        new { Name = resetDurationItem.CategoryName, IsEnabled = basicTimer && !shortTimer },
        new { Name = endEarlyPatternItem.CategoryName, IsEnabled = basicTimer && !shortTimer },
        new { Name = fontSizeItem.CategoryName, IsEnabled = overlay },
        new { Name = activeBrushItem.CategoryName, IsEnabled = overlayTimer },
        new { Name = idleBrushItem.CategoryName, IsEnabled = cooldownTimer },
        new { Name = assignedOverlaysItem.CategoryName, IsEnabled = overlayAssigned },
        new { Name = fadeDelayItem.CategoryName, IsEnabled = overlayText }
      });

      timerDurationItem.Visibility = (basicTimer && !shortTimer) ? Visibility.Visible : Visibility.Collapsed;
      timerShortDurationItem.Visibility = shortTimer ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ValueChanged(object sender, ValueChangedEventArgs args)
    {
      if (args.Property.Name != evalTimeItem.PropertyName &&
        args.Property.SelectedObject is TriggerPropertyModel trigger)
      {
        var triggerChange = true;
        var list = thePropertyGrid.Properties.ToList();
        var longestProp = PropertyGridUtil.FindProperty(list, evalTimeItem.PropertyName);

        var isValid = TriggerUtil.TestRegexProperty(trigger.UseRegex, trigger.Pattern, PatternEditor);
        isValid = isValid && TriggerUtil.TestRegexProperty(trigger.EndUseRegex, trigger.EndEarlyPattern, EndEarlyPatternEditor);
        isValid = isValid && TriggerUtil.TestRegexProperty(trigger.EndUseRegex2, trigger.EndEarlyPattern2, EndEarlyPattern2Editor);
        isValid = isValid && !string.IsNullOrEmpty(trigger.Pattern);

        if (args.Property.Name == patternItem.PropertyName)
        {
          trigger.WorstEvalTime = -1;
          longestProp.Value = -1;
        }
        else if (args.Property.Name == timerTypeItem.PropertyName && args.Property.Value is int timerType)
        {
          EnableCategories(true, timerType > 0, timerType == 2, false, false, true, false, false);
        }
        else if (args.Property.Name == triggerActiveBrushItem.PropertyName)
        {
          var original = trigger.Node.TriggerData;
          if (trigger.TriggerActiveBrush == null && original.ActiveColor == null)
          {
            triggerChange = false;
          }
          else
          {
            triggerChange = (trigger.TriggerActiveBrush == null && original.ActiveColor != null) ||
              (trigger.TriggerActiveBrush != null && original.ActiveColor == null) ||
              (trigger.TriggerActiveBrush.Color.ToHexString() != original.ActiveColor);
          }
        }
        else if (args.Property.Name == triggerFontBrushItem.PropertyName)
        {
          var original = trigger.Node.TriggerData;
          if (trigger.TriggerFontBrush == null && original.FontColor == null)
          {
            triggerChange = false;
          }
          else
          {
            triggerChange = (trigger.TriggerFontBrush == null && original.FontColor != null) ||
              (trigger.TriggerFontBrush != null && original.FontColor == null) ||
              (trigger.TriggerFontBrush.Color.ToHexString() != original.FontColor);
          }
        }
        else if (args.Property.Name == "DurationTimeSpan" && timerDurationItem.Visibility == Visibility.Collapsed)
        {
          triggerChange = false;
        }

        if (triggerChange)
        {
          saveButton.IsEnabled = isValid;
          cancelButton.IsEnabled = true;
        }
      }
      else if (args.Property.SelectedObject is TextOverlayPropertyModel textOverlay)
      {
        var textChange = true;
        var original = textOverlay.Node.OverlayData;

        if (args.Property.Name == overlayBrushItem.PropertyName)
        {
          textChange = !(textOverlay.OverlayBrush.Color.ToHexString() == original.OverlayColor);
          Application.Current.Resources["OverlayBrushColor-" + textOverlay.Node.Id] = textOverlay.OverlayBrush;
        }
        else if (args.Property.Name == fontBrushItem.PropertyName)
        {
          textChange = !(textOverlay.FontBrush.Color.ToHexString() == original.FontColor);
          Application.Current.Resources["TextOverlayFontColor-" + textOverlay.Node.Id] = textOverlay.FontBrush;
        }
        else if (args.Property.Name == fontFamilyItem.PropertyName)
        {
          textChange = textOverlay.FontFamily != original.FontFamily;
          Application.Current.Resources["TextOverlayFontFamily-" + textOverlay.Node.Id] = new FontFamily(textOverlay.FontFamily);
        }
        else if (args.Property.Name == fontSizeItem.PropertyName && textOverlay.FontSize.Split("pt") is string[] split && split.Length == 2
         && double.TryParse(split[0], out var newFontSize))
        {
          textChange = textOverlay.FontSize != original.FontSize;
          Application.Current.Resources["TextOverlayFontSize-" + textOverlay.Node.Id] = newFontSize;
        }

        if (textChange)
        {
          saveButton.IsEnabled = true;
          cancelButton.IsEnabled = true;
        }
      }
      else if (args.Property.SelectedObject is TimerOverlayPropertyModel timerOverlay)
      {
        var timerChange = true;
        var original = timerOverlay.Node.OverlayData;

        if (args.Property.Name == overlayBrushItem.PropertyName)
        {
          timerChange = !(timerOverlay.OverlayBrush.Color.ToHexString() == original.OverlayColor);
          Application.Current.Resources["OverlayBrushColor-" + timerOverlay.Node.Id] = timerOverlay.OverlayBrush;
        }
        else if (args.Property.Name == activeBrushItem.PropertyName)
        {
          timerChange = !(timerOverlay.ActiveBrush.Color.ToHexString() == original.ActiveColor);
          Application.Current.Resources["TimerBarActiveColor-" + timerOverlay.Node.Id] = timerOverlay.ActiveBrush;
        }
        else if (args.Property.Name == idleBrushItem.PropertyName)
        {
          timerChange = !(timerOverlay.IdleBrush.Color.ToHexString() == original.IdleColor);
          Application.Current.Resources["TimerBarIdleColor-" + timerOverlay.Node.Id] = timerOverlay.IdleBrush;
        }
        else if (args.Property.Name == resetBrushItem.PropertyName)
        {
          timerChange = !(timerOverlay.ResetBrush.Color.ToHexString() == original.ResetColor);
          Application.Current.Resources["TimerBarResetColor-" + timerOverlay.Node.Id] = timerOverlay.ResetBrush;
        }
        else if (args.Property.Name == backgroundBrushItem.PropertyName)
        {
          timerChange = !(timerOverlay.BackgroundBrush.Color.ToHexString() == original.BackgroundColor);
          Application.Current.Resources["TimerBarTrackColor-" + timerOverlay.Node.Id] = timerOverlay.BackgroundBrush;
        }
        else if (args.Property.Name == fontBrushItem.PropertyName)
        {
          timerChange = !(timerOverlay.FontBrush.Color.ToHexString() == original.FontColor);
          Application.Current.Resources["TimerBarFontColor-" + timerOverlay.Node.Id] = timerOverlay.FontBrush;
        }
        else if (args.Property.Name == fontSizeItem.PropertyName && timerOverlay.FontSize.Split("pt") is string[] split && split.Length == 2
         && double.TryParse(split[0], out var newFontSize))
        {
          timerChange = timerOverlay.FontSize != original.FontSize;
          Application.Current.Resources["TimerBarFontSize-" + timerOverlay.Node.Id] = newFontSize;
          Application.Current.Resources["TimerBarHeight-" + timerOverlay.Node.Id] = TriggerUtil.GetTimerBarHeight(newFontSize);
        }
        else if (args.Property.Name == timerModeItem.PropertyName)
        {
          PropertyGridUtil.EnableCategories(thePropertyGrid, new[] { new { Name = idleBrushItem.CategoryName, IsEnabled = (int)args.Property.Value == 1 } });
        }

        if (timerChange)
        {
          saveButton.IsEnabled = true;
          cancelButton.IsEnabled = true;
        }
      }
    }

    private void ShowClick(object sender, RoutedEventArgs e)
    {
      dynamic model = thePropertyGrid?.SelectedObject;
      if ((model is TimerOverlayPropertyModel || model is TextOverlayPropertyModel) && model?.Node?.Id is string id)
      {
        if (!PreviewWindows.TryGetValue(id, out var window))
        {
          PreviewWindows[id] = (model is TimerOverlayPropertyModel) ? new TimerOverlayWindow(model.Node, PreviewWindows)
            : new TextOverlayWindow(model.Node, PreviewWindows);
          PreviewWindows[id].Show();
        }
        else
        {
          window.Close();
          PreviewWindows.Remove(id, out _);
        }
      }
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
      dynamic model = thePropertyGrid?.SelectedObject;
      if (model is TriggerPropertyModel)
      {
        TriggerUtil.Copy(model.Node.TriggerData, model);
        TriggerStateManager.Instance.Update(model.Node);

        // reload triggers if current one is enabled by anyone
        if (TriggerStateManager.Instance.IsAnyEnabled(model.Node.Id))
        {
          TriggerManager.Instance.TriggersUpdated();
        }
      }
      else if (model is TextOverlayPropertyModel || model is TimerOverlayPropertyModel)
      {
        // only close overlay if non-style attributes have changed
        var old = model.Node.OverlayData as Overlay;
        if (old.Top != model.Top || old.Left != model.Left || old.Height != model.Height || old.Width != model.Width)
        {
          TriggerManager.Instance.CloseOverlay(model.Node.Id);
        }

        // if this overlay is changing to default and it wasn't previously then need to refresh Overlay tree
        var needRefresh = model.IsDefault && (old.IsDefault != model.IsDefault);

        TriggerUtil.Copy(model.Node.OverlayData, model);
        TriggerStateManager.Instance.Update(model.Node);

        // if this node is a default then refresh 
        if (needRefresh)
        {
          theTreeView.RefreshOverlays();
        }
      }

      cancelButton.IsEnabled = false;
      saveButton.IsEnabled = false;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
      dynamic model = thePropertyGrid?.SelectedObject;
      if (model is TriggerPropertyModel)
      {
        TriggerUtil.Copy(model, model.Node.TriggerData);
        var timerType = model.Node.TriggerData.TimerType;
        EnableCategories(true, timerType > 0, timerType == 2, false, false, true, false, false);
      }
      else if (model is TimerOverlayPropertyModel || model is TextOverlayPropertyModel)
      {
        TriggerUtil.Copy(model, model.Node.OverlayData);
      }

      thePropertyGrid.RefreshPropertygrid();
      Dispatcher.InvokeAsync(() => cancelButton.IsEnabled = saveButton.IsEnabled = false, DispatcherPriority.Background);
    }

    private void TriggerUpdateEvent(TriggerNode node)
    {
      if (node?.OverlayData is Overlay overlay)
      {
        var wasEnabled = saveButton.IsEnabled;
        TopEditor.Update(overlay.Top);
        LeftEditor.Update(overlay.Left);
        WidthEditor.Update(overlay.Width);
        HeightEditor.Update(overlay.Height);

        if (!wasEnabled)
        {
          saveButton.IsEnabled = false;
          cancelButton.IsEnabled = false;
        }
      }
    }

    private void TreeSelectionChangedEvent(Tuple<TriggerTreeViewNode, object> data)
    {
      var isTimerOverlay = data.Item1?.SerializedData?.OverlayData?.IsTimerOverlay == true;
      var isCooldownOverlay = isTimerOverlay && (data.Item1?.SerializedData?.OverlayData?.TimerMode == 1);

      saveButton.IsEnabled = false;
      cancelButton.IsEnabled = false;
      thePropertyGrid.SelectedObject = data.Item2;
      thePropertyGrid.IsEnabled = thePropertyGrid.SelectedObject != null;
      thePropertyGrid.DescriptionPanelVisibility = (data.Item1.IsTrigger() || data.Item1.IsOverlay()) ? Visibility.Visible : Visibility.Collapsed;
      showButton.Visibility = data.Item1.IsOverlay() ? Visibility.Visible : Visibility.Collapsed;

      if (data.Item1.IsTrigger())
      {
        var timerType = data.Item1.SerializedData.TriggerData.TimerType;
        EnableCategories(true, timerType > 0, timerType == 2, false, false, true, false, false);
      }
      else if (data.Item1.IsOverlay())
      {
        if (isTimerOverlay)
        {
          EnableCategories(false, false, false, true, true, false, false, isCooldownOverlay);
        }
        else
        {
          EnableCategories(false, false, false, true, false, false, true, false);
        }
      }
    }

    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        disposedValue = true;
        PreviewWindows.Values.ToList().ForEach(window => window.Close());
        PreviewWindows.Clear();
        TriggerStateManager.Instance.TriggerUpdateEvent -= TriggerUpdateEvent;
        TriggerStateManager.Instance.TriggerConfigUpdateEvent -= TriggerConfigUpdateEvent;
        TriggerStateManager.Instance.DeleteEvent -= TriggerOverlayDeleteEvent;
        theTreeView.TreeSelectionChangedEvent -= TreeSelectionChangedEvent;
        TestSynth?.Dispose();
        Watcher?.Dispose();
        thePropertyGrid?.Dispose();
        theTreeView.Dispose();
      }
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
      // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
      Dispose(true);
      // TODO: uncomment the following line if the finalizer is overridden above.
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
