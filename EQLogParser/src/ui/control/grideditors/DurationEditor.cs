﻿using Syncfusion.Windows.PropertyGrid;
using Syncfusion.Windows.Shared;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Data;

namespace EQLogParser
{
  internal class DurationEditor : ITypeEditor
  {
    private readonly List<TimeSpanEdit> TheTimeSpans = new List<TimeSpanEdit>();

    public void Attach(PropertyViewItem property, PropertyItem info)
    {
      Binding binding = new Binding("Value")
      {
        Mode = info.CanWrite ? BindingMode.TwoWay : BindingMode.OneWay,
        Source = info,
        ValidatesOnExceptions = true,
        ValidatesOnDataErrors = true
      };

      BindingOperations.SetBinding(TheTimeSpans.Last(), TimeSpanEdit.ValueProperty, binding);
    }

    public object Create(PropertyInfo propertyInfo) => Create();
    public object Create(PropertyDescriptor descriotor) => Create();

    private object Create()
    {
      var timeSpan = new TimeSpanEdit
      {
        MinValue = new System.TimeSpan(0, 0, 0),
        Format = "mm:ss"
      };

      TheTimeSpans.Add(timeSpan);
      return timeSpan;
    }

    public void Detach(PropertyViewItem property)
    {
      TheTimeSpans.ForEach(timeSpan =>
      {
        BindingOperations.ClearAllBindings(timeSpan);
        timeSpan?.Dispose();
      });

      TheTimeSpans.Clear();
    }
  }
}
