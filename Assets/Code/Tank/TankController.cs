﻿using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using TMPro;

public class TankController : MonoBehaviour
{

    public enum TankState
    {
        normal,
        destroyed
    }
    private float barrelRotateSpeed = 2.5f;
    private float reorientateSpeed = 10f;
    private float fireInterval = 2;
    private float projectileForce = 2000;
    private readonly int startingHealth = 10;
    private readonly int startingAmmo = 10;
    private const int SnitchPoints = 10;
    private const int speed = 10;
    private const int turnRate = 100;
    public bool infiniteHealth = false;
    public bool infiniteAmmo = false;
    public Color mainTankColor;
    public TankState currentState = TankState.normal;
    public volatile int Health;
    public volatile int Ammo;
    public volatile float X;
    public volatile float Y;
    public volatile float ForwardX;
    public volatile float ForwardY;
    public volatile float Heading;
    public volatile float TurretHeading;
    public volatile float TurretForwardX;
    public volatile float TurretForwardY;
    public string Token;
    public string Name;
    public int Points;
    public int UnbankedPoints;
    public GameObject root;
    public GameObject turret;
    public GameObject barrel;
    public GameObject firingPoint;
    private DateTime lastFireTime = DateTime.Now;
    private DateTime destroyTime;
    private bool toggleForward;
    private bool toggleReverse;
    private bool toggleLeft;
    private bool toggleRight;
    private bool toggleTurretRight;
    private bool toggleTurretLeft;
    private ParticleSystem smokeParticleSystem;
    public GameSimRules Ruleset { get; internal set; }
    public GameSimulation Sim { get; internal set; }
    public int Deaths { get; internal set; }
    GameObject uiLabel;
    AudioSource fireSound;
    AudioSource shellHit;
    AudioSource tankExplosion;
    private List<GameObject> pointObjects;
    private bool autoTurn = false;
    private bool autoTurretTurn = false;
    private bool autoMove = false;
    private float desiredHeading = 0;
    private float desiredTurretHeading = 0;
    private float desiredDistance = 0;
    private Vector3 oldPosition;

    // Use this for initialization
    public virtual void Start()
    {
        root = this.gameObject;
        turret = root.transform.Find("top").gameObject;
        barrel = turret.transform.Find("barrel").gameObject;
        firingPoint = barrel.transform.Find("firingpoint").gameObject;

        uiLabel = Instantiate(Resources.Load("Prefabs/TextLabel")) as UnityEngine.GameObject;

        uiLabel.GetComponent<TextMeshPro>().text = Name;
        
        Health = startingHealth;
        Ammo = startingAmmo;
        smokeParticleSystem = root.transform.Find("main").gameObject.GetComponent<ParticleSystem>();
        var em = smokeParticleSystem.emission;

        var sources = GetComponents<AudioSource>();

        fireSound = sources[0];
        shellHit = sources[1];
        tankExplosion = sources[2];


        em.enabled = false;

        pointObjects = new List<GameObject>();

    }

    // Update is called once per frame
    public virtual void Update()
    {


      
        if (currentState == TankState.normal)
        {
            if (toggleForward)
                Forward();
            if (toggleReverse)
                Reverse();
            if (toggleLeft)
                TurnLeft();
            if (toggleRight)
                TurnRight();
            if (toggleTurretLeft)
                TurretLeft();
            if (toggleTurretRight)
                TurretRight();


            if (autoTurn)
            {
                if (IsTurnLeft(Heading, desiredHeading))
                    TurnLeft();
                else
                    TurnRight();

                float diff = Heading - desiredHeading;
                if (Math.Abs(diff) < 2)
                    autoTurn = false;
            }

            if (autoTurretTurn)
            {
                if (IsTurnLeft(TurretHeading, desiredTurretHeading))
                    TurretLeft();
                else
                    TurretRight();

                float diff = TurretHeading - desiredTurretHeading;
                if (Math.Abs(diff) < 2)
                    autoTurretTurn = false;
            }

            if (autoMove)
            {
                if(desiredDistance > 0)
                {
                   

                    Forward();

                    float distanceTravelled = (oldPosition - transform.position).magnitude;
                    if (desiredDistance > distanceTravelled)
                    {
                        desiredDistance -= distanceTravelled;
                    }
                    else
                    {
                        autoMove = false;
                    }
                }
                else
                {
                 
                    Reverse();

                    float distanceTravelled = (oldPosition - transform.position).magnitude;
                    if (Math.Abs(desiredDistance) > distanceTravelled)
                    {
                        desiredDistance += distanceTravelled;
                    }
                    else
                    {
                        autoMove = false;
                    }
                }


            }


            Quaternion q = Quaternion.FromToRotation(transform.up, Vector3.up) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, q, Time.deltaTime * reorientateSpeed);

            oldPosition = transform.position;

        }
        else if (currentState == TankState.destroyed)
        {
            TimeSpan sinceDestruction = DateTime.Now - destroyTime;
            if (sinceDestruction.TotalSeconds > Ruleset.RespawnTime)
            {
                Sim.RespawnTank(this);
            }
        }


        if (Camera.current != null)
        {
            var pos = new Vector3(transform.position.x, 10, transform.position.z);

            Quaternion labelRotation = Quaternion.LookRotation(pos - Camera.current.transform.position, Camera.current.transform.up);
            uiLabel.transform.SetPositionAndRotation(pos, labelRotation);
        }

        X = transform.position.x;
        Y = transform.position.z;
        ForwardX = transform.forward.x;
        ForwardY = transform.forward.z;

        //A = atan2(V.y, V.x)
        Heading = (float)Math.Atan2(transform.forward.z, transform.forward.x);
        Heading = Heading * Mathf.Rad2Deg;
        Heading = (Heading - 360) % 360;
        Heading = Math.Abs(Heading);

        TurretHeading = (float)Math.Atan2(-turret.transform.up.z, -turret.transform.up.x);
        TurretHeading = TurretHeading * Mathf.Rad2Deg;
        TurretHeading = (TurretHeading - 360) % 360;
        TurretHeading = Math.Abs(TurretHeading);


        TurretForwardX = turret.transform.up.x;
        TurretForwardY = turret.transform.up.z;


        if (transform.position.y < -10)
        {
            DestroyTank();
        }


        if (infiniteAmmo)
            ReplenishAmmo();
        if (infiniteHealth)
            ReplenishHealth();

        int i = 0;
        foreach(GameObject o in pointObjects)
        {
            o.transform.position = transform.position + new Vector3(0, 5 + (i * 0.5f), 0);
            i++;
        }

    }

  

    

    internal void ReActivate()
    {
        currentState = TankState.normal;
        ReplenishHealthAndAmmo();
        Quaternion q = Quaternion.FromToRotation(transform.up, Vector3.up) * transform.rotation;
        transform.rotation = q;
        Debug.Log(Name + " Respawned ");

        var em = smokeParticleSystem.emission;
        em.enabled = false;
    }

    public void ReplenishHealthAndAmmo()
    {
        ReplenishAmmo();
        ReplenishHealth();
    }

    public void ReplenishHealth()
    {
        Health = startingHealth;
    }

    public void ReplenishAmmo()
    {
        Ammo = startingAmmo;
    }

    void OnCollisionEnter(Collision collision)
    {
        Console.WriteLine("Tank collision detected: " + collision.gameObject.name);

        var go = collision.collider.gameObject;
        if (go.tag.Contains("projectile"))
        {
          

            CalculateDamage(go);
            UnityEngine.GameObject.Destroy(go);


            var bulletExplosivo = Instantiate(Resources.Load("Prefabs/ShellExplosion")) as UnityEngine.GameObject;
            bulletExplosivo.transform.position = go.transform.position;

            shellHit.Play();
        }


    }

    private void CalculateDamage(UnityEngine.GameObject go)
    {
        //for now, all hits subtract one health
        Health--;

        if (Health <= 0)
        {
            Sim.RecordFrag(this, go.GetComponent<ProjectileState>().OwningTank);
            DestroyTank();
            Debug.Log(Name + " Destroyed ");
        }
    }

    private void DestroyTank()
    {
        if (currentState == TankState.destroyed)
            return;

        currentState = TankState.destroyed;
        FullStop();
        StopTurret();

        ClearUnbankedPoints();

        var explosivo1 = Instantiate(Resources.Load("Prefabs/Explosion")) as UnityEngine.GameObject;
        explosivo1.transform.position = transform.position;

        var explosivo2 = Instantiate(Resources.Load("Prefabs/TankExplosion")) as UnityEngine.GameObject;
        explosivo2.transform.position = transform.position;

        var em = smokeParticleSystem.emission;
        em.enabled = true;

        tankExplosion.Play();

        destroyTime = DateTime.Now;
    }

    public void AddUnbankedPoint()
    {
        UnbankedPoints++;
        var ob = GameObject.Instantiate(Resources.Load("Prefabs/UnbankedPoint") as GameObject);
        pointObjects.Add(ob);
    }

    public void ToggleForward()
    {
        toggleForward = true;
        toggleReverse = false;
    }
    public void ToggleReverse()
    {
        toggleForward = false;
        toggleReverse = true;
    }
    public void ToggleLeft()
    {
        toggleLeft = true;
        toggleRight = false;
    }
    public void ToggleRight()
    {
        toggleLeft = false;
        toggleRight = true;
    }
    public void ToggleTurretRight()
    {
        toggleTurretLeft = false;
        toggleTurretRight = true;
    }
    public void ToggleTurretLeft()
    {
        toggleTurretLeft = true;
        toggleTurretRight = false;
    }

    public void StopTurn()
    {
        toggleLeft = false;
        toggleRight = false;
        autoTurn = false;
      
    }

    public void StopMove()
    {
        toggleForward = false;
        toggleReverse = false;
    }

    public void FullStop()
    {
        toggleForward = false;
        toggleReverse = false;
        toggleLeft = false;
        toggleRight = false;
        autoTurretTurn = false;
        autoTurn = false;
    }

    public void StopTurret()
    {
        toggleTurretLeft = false;
        toggleTurretRight = false;
    }
    public void Forward()
    {
        if (root == null)
        {
            Console.WriteLine("Root ref null");
            return;
        }
        else
            Console.WriteLine("Root ref fixed");

        if (transform.position.y > -1.75f)
            return;

        root.transform.position = root.transform.position + root.transform.forward * Time.deltaTime * speed;
      
    }
    public void Reverse()
    {
        if (transform.position.y > -1.75f)
            return;

        root.transform.position = root.transform.position - root.transform.forward * Time.deltaTime * speed;

    }
    public void TurnRight()
    {
        if (transform.position.y > -1.75f)
            return;
        root.transform.Rotate(Vector3.up, turnRate * Time.deltaTime);


    }
    public void TurnLeft()
    {
        if (transform.position.y > -1.75f)
            return;

        root.transform.Rotate(Vector3.up, -turnRate * Time.deltaTime);
        
    }
    public void TurretLeft()
    {
        turret.transform.RotateAround(transform.up, -barrelRotateSpeed * Time.deltaTime);
    }
    public void TurretRight()
    {

        turret.transform.RotateAround(transform.up, barrelRotateSpeed * Time.deltaTime);
    }

    bool IsTurnLeft(float currentHeading, float desiredHeading)
    {
        float diff = desiredHeading - currentHeading;
        return diff > 0 ? diff > 180 : diff >= -180;
    }


    public void TurnTurretToHeading(float heading)
    {
        desiredTurretHeading = heading;
        autoTurretTurn = true;
    }

    public void TurnToHeading(float heading)
    {
        desiredHeading = heading;
        autoTurn = true;
    }

    internal void MoveDistance(float amount)
    {
        autoMove = true;
        desiredDistance = amount;
    }

    

    public void Fire()
    {
        TimeSpan timeSinceLastFired = DateTime.Now - lastFireTime;
        if (timeSinceLastFired.TotalSeconds < fireInterval)
            return;

        if (Ammo < 1)
            return;

        lastFireTime = DateTime.Now;
        UnityEngine.GameObject projectile = Instantiate(Resources.Load("Prefabs/Projectile")) as UnityEngine.GameObject;
        projectile.GetComponent<ProjectileState>().OwningTank = this;
        projectile.transform.position = firingPoint.transform.position;
        projectile.GetComponent<TrailRenderer>().enabled = true;
        projectile.GetComponent<TrailRenderer>().Clear();


        projectile.GetComponent<Rigidbody>().AddForce(-barrel.transform.up * projectileForce);
        projectile.GetComponent<TrailRenderer>().startColor = Color.blue;
        projectile.GetComponent<TrailRenderer>().endColor = Color.blue;

        GameObject.Destroy(projectile, 10);

        Ammo--;
        
        fireSound.Play();

    }

    private void OnDestroy()
    {
        GameObject.Destroy(uiLabel);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "GoalA" || other.gameObject.name == "GoalB")
        {
            Points += UnbankedPoints;

            ClearUnbankedPoints();

            EventManager.goalEvent.Invoke(this);

            //do we have a snitch?
            if (Sim.snitch != null)
            {
                if (Sim.snitch.GetComponent<SnitchBehaviour>().collector != null)
                {
                    if (Sim.snitch.GetComponent<SnitchBehaviour>().collector == this)
                    {
                        //we took the snitch to a goal!
                        Debug.Log("SNITCH COLLECTED SUCCESSFULLY!");
                        Points += SnitchPoints;
                        GameObject.Destroy(Sim.snitch);
                    }
                }
            }

        }
    }

    private void ClearUnbankedPoints()
    {
        UnbankedPoints = 0;

        //remove unbanked point objects
        foreach (GameObject o in pointObjects)
        {
            GameObject.Destroy(o);
        }
        pointObjects.Clear();
    }

   
}




