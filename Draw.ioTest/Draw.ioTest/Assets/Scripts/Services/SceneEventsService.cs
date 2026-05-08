using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneEventsService : ISceneEventsService
{
    public event Action OnAwake;
    public event Action OnStart;
    public event Action OnClean;
    public event Action OnUpdate;

    public void TriggerOnAwake()
    {
        OnAwake?.Invoke();
    }

    public void TriggerOnStart()
    {
        OnStart?.Invoke();
    }

    public void TriggerOnClean()
    {
        OnClean?.Invoke();
    }

    public void TriggerOnUpdate()
    {
        OnUpdate?.Invoke();
    }
}
