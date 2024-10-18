using System;

public class LerpUIAction
{
    public Action actions;

    public void Add(Action _action)
    {
        actions -= _action;
        actions += _action;
    }

    public void Remove(Action _action)
    {
        actions -= _action;
    }
}
