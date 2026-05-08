using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class SceneLoadObject : MonoBehaviour
{
    private ISceneEventsService m_SceneEventsService;
    
    [Inject]
    public void Construct(ISceneEventsService sceneEventsService)
    {
        m_SceneEventsService = sceneEventsService;
    }

    private void Awake()
    {
        m_SceneEventsService.TriggerOnAwake();
    }

    private void Start()
    {
        m_SceneEventsService.TriggerOnStart();
    }

    private void Update()
    {
        m_SceneEventsService.TriggerOnUpdate();
    }
}
