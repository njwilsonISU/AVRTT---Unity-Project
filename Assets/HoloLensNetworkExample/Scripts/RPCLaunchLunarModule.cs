using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Networking;

public class RPCLaunchLunarModule : MonoBehaviourPunCallbacks, IInRoomCallbacks
{
    public float thrust;
    public Rigidbody rb;
    public bool ThrustOn;
    public GameObject[] gameObjectArray;

    private PhotonView photonView1;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private TogglePlacementHints ToggleHints;
  

    public Transform objectToPlace;
    public Transform locationToPlace;

    public AudioSource audioSource;
    public GameObject toolTipObject;

    public float nearDistance = 0.1f;
    public float farDistance = 0.2f;

    bool isSnapped;

    private Vector3 originalObjectPlacementPosition;
    private Quaternion originalObjectPlacementRotation;
    public Transform originalParentObject;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        photonView1 = GetComponent<PhotonView>();

        originalPosition = transform.position;
        originalRotation = transform.rotation;

        originalObjectPlacementPosition = objectToPlace.localPosition;
        originalObjectPlacementRotation = objectToPlace.localRotation;
        originalParentObject = objectToPlace.transform.parent;

        ToggleHints = GetComponent<TogglePlacementHints>();

        photonView1.RPC("PartAssembly", RpcTarget.All);
        
       
    }

    [PunRPC]
    private void resetModule()
    {

        StopThurster();
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        transform.position = originalPosition;
        transform.rotation = originalRotation;
    }

    [PunRPC]
    private void StartThurster()
    {

        StartCoroutine(Thruster());
    }

    private void StopThurster()
    {
        ThrustOn = false;
    }

    private IEnumerator Thruster()
    {
        rb.isKinematic = false;

        ThrustOn = true;

        yield return null;

        while (ThrustOn)
        {
            yield return new WaitForSeconds(0.01f);
            rb.AddForce(transform.up * thrust);
        }

    }

    [PunRPC]
    private void ToggleGameObjects1()
    {
        ToggleHints.ToggleGameObjects();

    }


    [PunRPC]
    private void PartAssembly()
    {
        StartCoroutine(checkForSnap());

    }

    public void ResetPlacement()
    {
        objectToPlace.transform.parent = originalParentObject;
        //reset object placement
        objectToPlace.localPosition = originalObjectPlacementPosition;
        objectToPlace.localRotation = originalObjectPlacementRotation;

        //turn on tool tips again
        toolTipObject.SetActive(true);
    }

    IEnumerator checkForSnap()
    {
        while (true)
        {
            

            yield return new WaitForSeconds(0.01f);

            if (!isSnapped && Vector3.Distance(objectToPlace.position, locationToPlace.position) > 0.01 && Vector3.Distance(objectToPlace.position, locationToPlace.position) < nearDistance)
            {

                //Place object at target location
                objectToPlace.position = locationToPlace.position;
                objectToPlace.rotation = locationToPlace.rotation;
               

                //Set parent to target location so that when rocket launches, parts go with it
                photonView1.RPC("ChangeParent", RpcTarget.All);
           

                //Play audio snapping sound
                //TODO: Need to take into account whether manipulation handler is currently being held
                //if (!audioSource.isPlaying)


                //turn off tool tips
                toolTipObject.SetActive(false);

                //isSnapped = true;          

            }

            if (isSnapped && Vector3.Distance(objectToPlace.position, locationToPlace.position) > farDistance)
            {
                isSnapped = false;
            }
        }
    }

    [PunRPC]
    private void ChangeParent()
    {
        objectToPlace.SetParent(locationToPlace.parent);
    }

    public void launch()
    {
        photonView1.RPC("StartThurster", RpcTarget.Others);
        
    }

    public void reset()
    {
        photonView1.RPC("resetModule", RpcTarget.Others);
    }

    public void hints()
    {
        photonView1.RPC("ToggleGameObjects1", RpcTarget.Others);
    }



}
