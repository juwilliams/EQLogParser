﻿using System;
using System.Collections.Generic;
using System.Windows;

namespace EQLogParser
{
  /// <summary>
  /// Interaction logic for TankingBreakdown.xaml
  /// </summary>
  public partial class TankingBreakdown : BreakdownTable, IDisposable
  {
    private List<PlayerStats> PlayerStats = null;

    public TankingBreakdown()
    {
      InitializeComponent();
      InitBreakdownTable(dataGrid, selectedColumns);
    }

    internal void Init(CombinedStats currentStats, List<PlayerStats> selectedStats)
    {
      titleLabel.Content = currentStats?.ShortTitle;
      PlayerStats = selectedStats;
      Display();
    }

    private void CopyCsvClick(object sender, RoutedEventArgs e) => DataGridUtil.CopyCsvFromTable(dataGrid, titleLabel.Content.ToString());
    private void CreateImageClick(object sender, RoutedEventArgs e) => DataGridUtil.CreateImage(dataGrid, titleLabel);

    internal void Display()
    {
      dataGrid.ItemsSource = null;
      dataGrid.ItemsSource = PlayerStats;
    }

    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        dataGrid.Dispose();
        disposedValue = true;
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
