using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Each modal owns a lease. Releasing one cannot unpause another.</summary>
public static class RunPause
{
    sealed class Lease : IDisposable
    {
        public bool cursor;
        public void Dispose() { if(owners.Remove(this))Refresh(); }
    }
    static readonly List<Lease> owners=new List<Lease>();
    static float resumeScale=1;
    public static bool IsPaused=>owners.Count>0;
    public static IDisposable Acquire(bool cursor)
    {
        if(owners.Count==0)resumeScale=Time.timeScale>0?Time.timeScale:1;
        var lease=new Lease{cursor=cursor};owners.Add(lease);Refresh();return lease;
    }
    static void Refresh()
    {
        Time.timeScale=IsPaused?0:resumeScale;
        bool show=owners.Exists(o=>o.cursor);
        Cursor.lockState=show?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=show;
    }
    public static void Reset()
    { owners.Clear();resumeScale=1;Time.timeScale=1; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Boot() { Reset(); }
}
