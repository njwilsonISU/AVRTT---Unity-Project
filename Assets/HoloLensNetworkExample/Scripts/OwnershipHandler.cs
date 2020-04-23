using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.Input;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class OwnershipHandler : MonoBehaviourPun, IPunOwnershipCallbacks, IMixedRealityInputHandler
{
    private PhotonView PV;

    // Start is called before the first frame update
    void Start()
    {
        PV = GetComponent<PhotonView>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
    {
        Debug.Log("OnOwnershipRequest");
        
        targetView.TransferOwnership(requestingPlayer);
    }

    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        Debug.Log("OwnerShip Transfered from " + previousOwner.UserId);
    }

    void TransferControl(Player idPlayer)
    {
        Debug.Log("TransferControl");
        if (PV.IsMine)
        {
            PV.TransferOwnership(idPlayer);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (PV != null)
        {
            Debug.Log("OnTriggerEnter RequestOwnerShip");
            this.PV.RequestOwnership();

        }
    }

    private void OnTriggerExit(Collider other)
    {

    }

    public void OnInputUp(InputEventData eventData)
    {
       
    }

    public void OnInputDown(InputEventData eventData)
    {
        Debug.Log("OnInputDown");
        this.PV.RequestOwnership();
    }
}
