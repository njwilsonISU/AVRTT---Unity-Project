using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SpatialAnchors.Unity.Samples;
using Microsoft.Azure.SpatialAnchors.Unity;
using Microsoft.Azure.SpatialAnchors;
using System;
using System.IO;
using Photon.Pun;
using UnityEngine.Networking;
using RestSharp;
using ASASharingPlugin;

#if WINDOWS_UWP
using Windows.Storage;
#endif

public class AnchorModuleScript : MonoBehaviour
{

    //This is the name of the anchor ID to find anchors stored on Azure. Users cannot customize this at this time. This will be provided by Azure.
    public string AzureAnchorID = "";
    public string publicSharingPin = "";

    AzureSpatialAnchorsDemoWrapper CloudManager;
    CloudSpatialAnchor currentCloudAnchor;
    CloudSpatialAnchorWatcher currentWatcher;

    public ASASharing AnchorSharingClass = new ASASharing();

    private readonly Queue<Action> dispatchQueue = new Queue<Action>();

    private PhotonView PV;
    
    // Start is called before the first frame update
    void Start()
    {
        //This gets the AzureSpatialAnchorsDemoWrapper.cs component in your scene (must be present in scene)
        CloudManager = AzureSpatialAnchorsDemoWrapper.Instance;

        //The code below registers Azure Spatial Anchor events
        CloudManager.OnAnchorLocated += CloudManager_OnAnchorLocated;

        PV = GetComponent<PhotonView>();
        


    }

    void Update()
    {
        lock (dispatchQueue)
        {
            if (dispatchQueue.Count > 0)
            {
                dispatchQueue.Dequeue()();
            }
        }
    }

    public void StartAzureSession()
    {
        DebugWindowMessaging.Clear();
        CloudManager.ResetSessionStatusIndicators();
        CloudManager.EnableProcessing = true;
        Debug.Log("Azure Session Started!!");
    }

    public void StopAzureSession()
    {
        DebugWindowMessaging.Clear();
        CloudManager.EnableProcessing = false;
        CloudManager.ResetSession();
        Debug.Log("Azure Session Stopped!!");

    }

    public void CreateAzureAnchor(GameObject theObject)
    {
        DebugWindowMessaging.Clear();
        Debug.Log("Create_Azure_Anchor button is pressed");
        Debug.Log("Wait for sometime and press Share_Azure_Anchor");
        //First we create a local anchor at the location of the object in question
        theObject.AddARAnchor();

        //Then we create a new local cloud anchor
        CloudSpatialAnchor localCloudAnchor = new CloudSpatialAnchor();

        //Now we set the local cloud anchor's position to the local anchor's position
        localCloudAnchor.LocalAnchor = theObject.GetNativeAnchorPointer();

        //Check to see if we got the local anchor pointer
        if (localCloudAnchor.LocalAnchor == IntPtr.Zero)
        {
            Debug.Log("Didn't get the local XR anchor pointer...");
            return;
        }

        // In this sample app we delete the cloud anchor explicitly, but here we show how to set an anchor to expire automatically
        localCloudAnchor.Expiration = DateTimeOffset.Now.AddDays(7);

        //Save anchor to cloud
        Task.Run(async () =>
        {
            while (!CloudManager.EnoughDataToCreate)
            {
                await Task.Delay(330);
            }

            bool success = false;
            try
            {

                currentCloudAnchor = await CloudManager.StoreAnchorInCloud(localCloudAnchor);

                //Save the Azure Anchor ID
                GenericNetworkManager.instance.AzureAnchorID = currentCloudAnchor.Identifier;
                Debug.Log("Azure anchor ID saved!");

                success = currentCloudAnchor != null;
                localCloudAnchor = null;

                if (success)
                {
                    Debug.Log("Successfully Created Anchor");
                }

            }
            catch (Exception ex)
            {
                Debug.Log(ex);
                Debug.Log("Anchor creation failure");
            }
        });

    }

    public void RemoveLocalAnchor(GameObject theObject)
    {
        DebugWindowMessaging.Clear();
        theObject.RemoveARAnchor();
        Debug.Log("Local anchor removed");
    }

    //Start looking for specified anchors
    public void FindAzureAnchor(string AnchorIDtoFind)
    {
        DebugWindowMessaging.Clear(); DebugWindowMessaging.Clear();
        //Provide list of anchor IDs to locate
        SetUpAnchorIDsToLocate();

        //Start watching for Anchors
        currentWatcher = CloudManager.CreateWatcher();
        Debug.Log("Azure anchors found!");
    }

    public void DeleteAzureAnchor(string AnchorIDtoDelete)
    {
        DebugWindowMessaging.Clear();
        //Delete the anchor with the ID specified off the server and locally
        Task.Run(async () =>
        {
            await CloudManager.DeleteAnchorAsync(currentCloudAnchor);
            currentCloudAnchor = null;
        });
        Debug.Log("Azure anchor deleted");
    }

    public void SaveAzureAnchorIDToDisk(string AnchorIDtoSave = null)
    {
        String path = "";

        if (AnchorIDtoSave == null)
            AnchorIDtoSave = GenericNetworkManager.instance.AzureAnchorID;

#if WINDOWS_UWP
            StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
            path = storageFolder.Path.Replace('\\', '/') + "/";
        path = Path.Combine(path, "SavedAzureAnchorID.txt");
        File.WriteAllText(path, AnchorIDtoSave);              
#endif

        Debug.Log("Saved Anchor ID: " + AnchorIDtoSave + " to this path: " + path);

    }

    public void LoadAzureAnchorIDsFromDisk()
    {
        String path = "";
#if WINDOWS_UWP
            StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
            path = storageFolder.Path.Replace('\\', '/') + "/";
        path = Path.Combine(path, "SavedAzureAnchorID.txt");

        GenericNetworkManager.instance.AzureAnchorID = File.ReadAllText(path);

            
#endif

        Debug.Log("Loaded Azure Anchor ID from Disk: " + GenericNetworkManager.instance.AzureAnchorID + " from this path: " + path);
        Debug.Log("Azure anchor saved to disk!");
    }

    public void SetUpAnchorIDsToLocate()
    {
        List<string> anchorsToFind = new List<string>();

        if (GenericNetworkManager.instance.AzureAnchorID != "")
        {
            anchorsToFind.Add(GenericNetworkManager.instance.AzureAnchorID);
        }

        CloudManager.SetAnchorIdsToLocate(anchorsToFind);
    }

    private void CloudManager_OnAnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        Debug.LogFormat("Anchor recognized as a possible anchor {0} {1}", args.Identifier, args.Status);
        if (args.Status == LocateAnchorStatus.Located)
        {
            OnCloudAnchorLocated(args);
        }
    }

    private void OnCloudAnchorLocated(AnchorLocatedEventArgs args)
    {
        Debug.Log("Cloud Anchor Located:" + args.Status);

        if (args.Status == LocateAnchorStatus.Located)
        {
            currentCloudAnchor = args.Anchor;
            Debug.Log("Anchor ID Found: " + currentCloudAnchor.Identifier);

            QueueOnUpdate(() =>
            {
                Pose anchorPose = Pose.identity;

#if UNITY_ANDROID || UNITY_IOS
            anchorPose = currentCloudAnchor.GetAnchorPose();
#endif
                Debug.Log("Now setting gameObject to anchor position and rotation.");

                // HoloLens: The position will be set based on the unityARUserAnchor that was located.
#if WINDOWS_UWP || UNITY_WSA
                //create a local anchor at the location of the object in question
                gameObject.AddARAnchor();

                // On HoloLens, if we do not have a cloudAnchor already, we will have already positioned the
                // object based on the passed in worldPos/worldRot and attached a new world anchor,
                // so we are ready to commit the anchor to the cloud if requested.
                // If we do have a cloudAnchor, we will use it's pointer to setup the world anchor,
                // which will position the object automatically.
                if (currentCloudAnchor != null)
                {
                    Debug.Log("Setting Local Anchor to Cloud Anchor Position.");
                    gameObject.GetComponent<UnityEngine.XR.WSA.WorldAnchor>().SetNativeSpatialAnchorPtr(currentCloudAnchor.LocalAnchor);
                }
#else
                Debug.Log("Cloud anchor position: " + anchorPose.position + ". Cloud Anchor Rotation: " + anchorPose.rotation);
            SetObjectToAnchorPose(anchorPose.position, anchorPose.rotation);
            
#endif
            });

        }
    }

    private void SetObjectToAnchorPose(Vector3 position, Quaternion rotation)
    {
        Debug.Log("Setting Object To Anchor Pose");
        transform.position = position;
        transform.rotation = rotation;

        //create a local anchor at the location of the object in question
        gameObject.AddARAnchor();
    }

    void OnDestroy()
    {
        if (CloudManager != null)
        {
            CloudManager.EnableProcessing = false;
        }

        if (currentWatcher != null)
        {
            currentWatcher.Stop();
            currentWatcher = null;
        }

    }

    /// <summary>
    /// Queues the specified <see cref="Action"/> on update.
    /// </summary>
    /// <param name="updateAction">The update action.</param>
    protected void QueueOnUpdate(Action updateAction)
    {
        lock (dispatchQueue)
        {
            dispatchQueue.Enqueue(updateAction);
        }
    }

    public void ShareAnchorNetwork()
    {
        GameObject PVuser = GenericNetworkManager.instance.localUser.gameObject;
        PhotonUser pu = PVuser.gameObject.GetComponent<PhotonUser>();
        pu.PVShareAnchorNetwork();
        Debug.Log("AnchorModuleScript ShareAnchorNetwork - AzureAnchorID" + GenericNetworkManager.instance.AzureAnchorID);
        Debug.Log("Anchor network shared!!");
    }


    //public void ShareAnchorNetwork()
    //{
    //    PV = GenericNetworkManager.instance.localUser;
    //    DebugWindowMessaging.Clear();
    //    Debug.Log("ShareAnchorNetwork RPC - AzureAnchorID" + GenericNetworkManager.instance.AzureAnchorID);
    //    if (PV != null)
    //    {
    //        PV.RPC("RPC_SetSharedAnchorID", RpcTarget.AllBuffered, GenericNetworkManager.instance.AzureAnchorID);
    //        Debug.Log("AzureAnchorID user "  + " " +PV.Controller.UserId);
    //    }
    //    else
    //    {
    //        Debug.Log("PV is null");
    //    }
    //}

    public void GetSharedAnchorNetwork()
    {
        DebugWindowMessaging.Clear();
        Debug.Log("GetSharedAnchorNetwork - AzureAnchorID" + GenericNetworkManager.instance.AzureAnchorID);
        FindAzureAnchor(GenericNetworkManager.instance.AzureAnchorID);
        Debug.Log("Recieved anchor network!");
    }

    public void ShareAnchor()
    {

        AnchorSharingClass.ShareAnchor(publicSharingPin, AzureAnchorID);

    }

    public void GetSharedAzureAnchor()
    {
        StartCoroutine(AnchorSharingClass.GetSharedAzureAnchorCoroutine(publicSharingPin));
        StartCoroutine(waitForDownloadedAnchorID());
    }

    public IEnumerator waitForDownloadedAnchorID()
    {
        while (AnchorSharingClass.DownloadedAzureAnchorID == "")
        {
            yield return null;
        }

        GenericNetworkManager.instance.AzureAnchorID = AnchorSharingClass.DownloadedAzureAnchorID;

    }
}
