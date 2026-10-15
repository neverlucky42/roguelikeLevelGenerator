using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
public class CellAutomata : IEnumerator<int[,]>, IEnumerable<int[,]>
{
    private int[,] _grid;
    private int _maxIterations;
    private int _currentIteration = 0;
    private Vector2Int _size;
    public int MaxIterations { get => _maxIterations; }
    public Vector2Int Size { get => _size; }
    public int[,] Current { get => _grid; }

    object IEnumerator.Current => Current;
    public CellAutomata(int maxIterations, Vector2Int size)
    {
        _maxIterations = maxIterations;
        _size = size;
        _grid = new int[size.x, size.y];

    }
    public bool MoveNext()
    {
        _currentIteration++;
        return _currentIteration >= _maxIterations;
    }
    public void Reset()
    {
        _currentIteration = 0;
    }
    void IDisposable.Dispose()
    {

    }

    public IEnumerator<int[,]> GetEnumerator()
    {
        return this;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

} 