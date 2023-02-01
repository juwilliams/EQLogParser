﻿using Syncfusion.Windows.PropertyGrid;
using Syncfusion.Windows.Shared;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Data;

namespace EQLogParser
{
  internal class ColorEditor : ITypeEditor
  {
    private readonly List<ColorPicker> TheColorPickers = new List<ColorPicker>();

    public void Attach(PropertyViewItem property, PropertyItem info)
    {
      Binding binding = new Binding("Value")
      {
        Mode = info.CanWrite ? BindingMode.TwoWay : BindingMode.OneWay,
        Source = info,
        ValidatesOnExceptions = true,
        ValidatesOnDataErrors = true
      };

      BindingOperations.SetBinding(TheColorPickers.Last(), ColorPicker.BrushProperty, binding);
    }

    public object Create(PropertyInfo propertyInfo) => Create();
    public object Create(PropertyDescriptor descriotor) => Create();

    private object Create()
    {
      var colorPicker = new ColorPicker { EnableSolidToGradientSwitch = false };
      TheColorPickers.Add(colorPicker);
      return colorPicker;
    }

    public void Detach(PropertyViewItem property)
    {
      TheColorPickers.ForEach(colorPicker =>
      {
        BindingOperations.ClearAllBindings(colorPicker);
        colorPicker?.Dispose();
      });

      TheColorPickers.Clear();
    }
  }
}
