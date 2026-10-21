using System;
using System.Collections.Generic;

namespace CivSim.Game.Ui;

/// <summary>One thing the player can dismiss with Esc: a panel, drawer, popover, modal or a mode.</summary>
public sealed class UiLayer
{
    public string Name { get; }
    public bool IsModal { get; }
    private readonly Func<bool> _close;

    public UiLayer(string name, bool isModal, Func<bool> close)
    {
        Name = name;
        IsModal = isModal;
        _close = close;
    }

    /// <summary>Closes the layer. Returns false when the layer handled Esc itself without closing (e.g. it collapsed an inline row).</summary>
    public bool Close() => _close();
}

/// <summary>LIFO stack of open layers. Esc always closes the topmost one (docs/MVP/20 §4.2).</summary>
public sealed class UiStack
{
    private readonly List<UiLayer> _layers = new(8);

    public int Count => _layers.Count;
    public UiLayer? Top => _layers.Count > 0 ? _layers[^1] : null;
    public bool TopIsModal => Top is { IsModal: true };
    public bool AnyModal
    {
        get
        {
            foreach (UiLayer layer in _layers) if (layer.IsModal) return true;
            return false;
        }
    }

    public bool Contains(UiLayer layer) => _layers.Contains(layer);

    public bool Contains(string name)
    {
        foreach (UiLayer layer in _layers) if (layer.Name == name) return true;
        return false;
    }

    public void Push(UiLayer layer)
    {
        _layers.Remove(layer);
        _layers.Add(layer);
    }

    public void Remove(UiLayer layer) => _layers.Remove(layer);

    /// <summary>Esc: closes the top layer. True when there was one.</summary>
    public bool CloseTop()
    {
        if (_layers.Count == 0) return false;
        UiLayer top = _layers[^1];
        if (top.Close()) _layers.Remove(top);
        return true;
    }
}
