using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISceneEventsService
{
    public event Action OnAwake;
    public event Action OnStart;
    public event Action OnClean;
    public event Action OnUpdate;

    public void TriggerOnAwake();
    public void TriggerOnStart();
    public void TriggerOnClean();
    public void TriggerOnUpdate();
}
