Stop-Process -Id 25732 -ErrorAction SilentlyContinue
$taskPath='D:\Unity\FPS-PayDayGame\Assets\Scripts\WeaponFeel\RobotPresentation.cs'
$taskText=[IO.File]::ReadAllText($taskPath).Replace("`r`n","`n")
$taskText=$taskText.Replace('    public Transform visual,muzzle,turret;','    public Transform visual,muzzle,turret;'+"`n"+'    [HideInInspector] public int rangedRigVersion;')
$taskStart=$taskText.IndexOf('    public void ConfigureRangedRig()')
$taskEnd=$taskText.IndexOf('    public void UpdateAim()', $taskStart)
$taskNew=@'
    public void ConfigureRangedRig()
    {
        if(turret==null||muzzle==null||rangedRigVersion>=1)return;
        // The source sentinel is authored looking down -Z; navigation uses +Z.
        visual.localRotation=Quaternion.Euler(0,180,0)*visual.localRotation;
        var imported=turret;
        if(turret.name=="Ranged aim pivot")
            foreach(Transform child in turret)if(child.name=="MachineGunRobot_GunJoint"){imported=child;break;}
        var barrel=imported.GetComponentInChildren<MeshFilter>();
        foreach(var mesh in imported.GetComponentsInChildren<MeshFilter>())
            if(mesh.name=="MachineGunRobot_Gun")barrel=mesh;
        Vector3 forward=transform.forward,tip=muzzle.position;
        if(barrel!=null&&barrel.sharedMesh!=null)
        {
            var bounds=barrel.sharedMesh.bounds;
            Vector3 axis=bounds.size.x>bounds.size.y&&bounds.size.x>bounds.size.z?Vector3.right:
                bounds.size.y>bounds.size.z?Vector3.up:Vector3.forward;
            if(Vector3.Dot(barrel.transform.TransformVector(axis),forward)<0)axis=-axis;
            forward=barrel.transform.TransformVector(axis).normalized;
            float length=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(axis.x),Mathf.Abs(axis.y),Mathf.Abs(axis.z)));
            tip=barrel.transform.TransformPoint(bounds.center+axis*length)+forward*.015f;
        }
        var pivot=turret.name=="Ranged aim pivot"?turret:new GameObject("Ranged aim pivot").transform;
        Vector3 joint=imported.position;
        imported.SetParent(visual,true);muzzle.SetParent(visual,true);
        pivot.SetParent(visual,true);pivot.position=joint;pivot.rotation=Quaternion.LookRotation(forward,transform.up);
        imported.SetParent(pivot,true);muzzle.SetParent(pivot,true);
        muzzle.position=tip;muzzle.rotation=pivot.rotation;
        turret=pivot;rangedRigVersion=1;
    }
'@
$taskText=$taskText.Substring(0,$taskStart)+$taskNew+"`n"+$taskText.Substring($taskEnd)
$taskText=$taskText.Replace('rotation,turretRotation,deathStart','rotation,deathStart').Replace('        if(turret!=null)turretRotation=turret.localRotation;'+"`n",'')
[IO.File]::WriteAllText($taskPath,$taskText,[Text.UTF8Encoding]::new($false))
$taskPath='D:\Unity\FPS-PayDayGame\Assets\Scripts\AI\EnemySignals.cs'
$taskText=[IO.File]::ReadAllText($taskPath).Replace("`r`n","`n")
$taskText=$taskText.Replace('        var assembly = Anchor(visual, "Overload dorsal capacitors", Vector3.zero);','        var assembly = Anchor(visual, "Overload dorsal capacitors", Vector3.zero);'+"`n"+'        assembly.rotation = transform.rotation;')
[IO.File]::WriteAllText($taskPath,$taskText,[Text.UTF8Encoding]::new($false))
$taskOut='D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\EditorAI'
$taskProcess=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe' -ArgumentList @('-batchmode','-projectPath','"D:\Unity\FPS-PayDayGame"','-executeMethod','EnemyPolishBuild.Play','-ai-qa','-ai-output','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\EditorAI"','-player-profile','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\editor-ai-profile.json"','-logFile','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\editor-ai.log"') -WindowStyle Hidden -PassThru
Write-Output "Editor AI process: $($taskProcess.Id)"