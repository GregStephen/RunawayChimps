using System;
using System.Reflection;
using RunawayChimps.SocialSafety;
using RunawayChimps.Travel;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using Photon.Voice.PUN;
using UnityEngine.SceneManagement;

static class Program
{
    static int assertions;
    static void Check(bool value,string name) { assertions++;if(!value)throw new Exception(name); }
    static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);
    static void Tick(float time) { Time.realtimeSinceStartup=Time.unscaledTime=time; }
    static Player Person(int actor,string id,bool local=false) { var p=new Player { ActorNumber=actor,IsLocal=local,NickName="Same name" };p.CustomProperties[PlayerSafetyService.AccountProperty]=id;p.CustomProperties[SectorPresence.PropertyKey]=1;return p; }
    static void Main()
    {
        var state=new PlayerSafetyState();
        for(int a=0;a<2;a++)for(int b=0;b<2;b++)for(int c=0;c<2;c++)
            Check(PlayerSafetyState.ShouldMute(a==1,b==1,c==1)==(a==1||b==0||c==1),"combined mute truth table");
        Check(!state.SetMuted(1,1,true)&&!state.SetMuted(0,1,true),"cannot mute self/invalid actor");
        state.SetMuted(2,1,true);Check(state.IsMuted(2)&&!state.IsMuted(3),"actor isolation");
        state.ChangeRoom();Check(!state.IsMuted(2),"room clears actor reuse");
        Check(!PlayerSafetyState.ValidAccountId("<abc>")&&!PlayerSafetyState.ValidAccountId(null)&&PlayerSafetyState.ValidAccountId("ABC019F"),"account format");
        Check(PlayerSafetyState.SafeName("<b>Bo\nn\u202Ee</b>")=="bBone/b","display sanitization");
        Check(PlayerSafetyState.SafeName(new string('W',100)).Length==24,"bounded name");

        var service=new GameObject().AddComponent<PlayerSafetyService>();Call(service,"Awake");
        Check(service.GetComponent<PlayerBoardHubPresenter>()!=null,"service installs Hub presenter");
        TestHubPresenter();
        var local=Person(1,"AAA",true);var aPlayer=Person(2,"BBB");var bPlayer=Person(3,"CCC");
        PhotonNetwork.LocalPlayer=local;PhotonNetwork.CurrentRoom=new Room();
        PhotonNetwork.CurrentRoom.Players[1]=local;PhotonNetwork.CurrentRoom.Players[2]=aPlayer;PhotonNetwork.CurrentRoom.Players[3]=bPlayer;
        Tick(10);service.SetLocalAccount("AAA");service.OnJoinedRoom();
        Check((string)local.CustomProperties[PlayerSafetyService.AccountProperty]=="AAA","publishes local report identity");
        service.ToggleMute(aPlayer);Check(PlayerSafetyService.IsMuted(aPlayer)&&!PlayerSafetyService.IsMuted(bPlayer),"duplicate-name mute isolation");
        Check(service.CaptureTarget(local)==null,"self report blocked");
        var target=service.CaptureTarget(aPlayer);service.Submit(target,PlayerReportReason.Harassment);
        Check(PlayFabClientAPI.Calls==1&&service.State.IsSending,"one request starts");
        service.Submit(target,PlayerReportReason.Other);Check(PlayFabClientAPI.Calls==1,"repeated press single flight");
        Check(PlayFabClientAPI.LastRequest.ReporteeId=="BBB"&&PlayFabClientAPI.LastRequest.Comment.Contains("Harassment"),"correct account and reason");
        PlayFabClientAPI.Success(new ReportPlayerClientResult {SubmissionsRemaining=4});
        Check(service.GetReportStatus(target)=="Report submitted. Thank you."&&!service.State.IsSending,"service receipt success");
        Tick(20);service.Submit(target,PlayerReportReason.Cheating);Check(PlayFabClientAPI.Calls==1,"duplicate success blocked");
        target=service.CaptureTarget(bPlayer);bPlayer.CustomProperties[PlayerSafetyService.AccountProperty]="BAD";
        service.Submit(target,PlayerReportReason.Other);Check(PlayFabClientAPI.Calls==1,"identity mutation rejected");
        bPlayer.CustomProperties[PlayerSafetyService.AccountProperty]="CCC";PhotonNetwork.CurrentRoom.Players.Remove(3);
        service.Submit(target,PlayerReportReason.Other);Check(PlayFabClientAPI.Calls==2,"departed target snapshot remains reportable");
        PlayFabClientAPI.Failure(new PlayFabError());Check(!service.State.IsSending&&!service.State.WasReported("CCC"),"failure permits retry");
        service.Submit(target,PlayerReportReason.Other);Check(PlayFabClientAPI.Calls==2,"retry cooldown");
        Tick(24);service.Submit(target,PlayerReportReason.Other);Check(PlayFabClientAPI.Calls==3,"explicit retry");
        PlayFabClientAPI.Success(new ReportPlayerClientResult {SubmissionsRemaining=0});
        Check(service.GetReportStatus(target).Contains("could not be confirmed")&&!service.State.WasReported("CCC"),"cap never becomes false success");
        Tick(30);service.Submit(target,PlayerReportReason.Other);var late=PlayFabClientAPI.Success;
        Tick(51);Call(service,"Update");Check(!service.State.IsSending&&service.GetReportStatus(target).Contains("No confirmation"),"bounded timeout");
        service.Submit(target,PlayerReportReason.Other);late(new ReportPlayerClientResult {SubmissionsRemaining=3});
        Check(service.State.IsSending,"late callback cannot finish newer request");
        var oldCallback=PlayFabClientAPI.Success;PhotonNetwork.CurrentRoom=new Room();service.OnJoinedRoom();
        oldCallback(new ReportPlayerClientResult {SubmissionsRemaining=3});
        Check(!service.State.IsSending&&!service.State.WasReported("CCC")&&!PlayerSafetyService.IsMuted(aPlayer),"room change invalidates state and callback");
        int calls=PlayFabClientAPI.Calls;service.Submit(target,PlayerReportReason.Other);Check(PlayFabClientAPI.Calls==calls,"stale dialog cannot submit into new room");
        PhotonNetwork.CurrentRoom.Players[1]=local;PhotonNetwork.CurrentRoom.Players[2]=aPlayer;
        target=service.CaptureTarget(aPlayer);PlayFabClientAPI.LoggedIn=false;Tick(70);service.Submit(target,PlayerReportReason.Other);
        Check(PlayFabClientAPI.Calls==calls,"signed out rejected");PlayFabClientAPI.LoggedIn=true;
        PlayFabClientAPI.Throws=true;service.Submit(target,PlayerReportReason.Other);
        Check(!service.State.IsSending,"synchronous service exception unlocks UI");PlayFabClientAPI.Throws=false;
        aPlayer.CustomProperties[PlayerSafetyService.AccountProperty]="AAA";target=service.CaptureTarget(aPlayer);Tick(80);
        service.Submit(target,PlayerReportReason.Other);Check(PlayFabClientAPI.Calls==calls,"own-account report rejected");
        aPlayer.CustomProperties[PlayerSafetyService.AccountProperty]=null;target=service.CaptureTarget(aPlayer);
        service.Submit(target,PlayerReportReason.Other);Check(PlayFabClientAPI.Calls==calls,"missing ID rejected without losing mute");

        // Execute the actual voice integration through changing sectors and speaker replacement.
        SectorTravelService.I=new SectorTravelService {CurrentSector=SectorId.Hub};
        var avatar=new GameObject();var view=avatar.AddComponent<PhotonView>();view.Owner=aPlayer;
        var voice=avatar.AddComponent<PhotonVoiceView>();var speaker=new GameObject().AddComponent<AudioSource>();voice.SpeakerInUse=speaker;
        var visibility=avatar.AddComponent<SectorAvatarVisibility>();Call(visibility,"Awake");Call(visibility,"LateUpdate");
        Check(!speaker.mute,"same sector initially audible");
        service.ToggleMute(aPlayer);Call(visibility,"LateUpdate");Check(speaker.mute,"per-player mute applied");
        SectorTravelService.I.CurrentSector=SectorId.Containment;Call(visibility,"LateUpdate");
        service.ToggleMute(aPlayer);Call(visibility,"LateUpdate");Check(speaker.mute,"unmute cannot override cross-sector gate");
        SectorTravelService.I.CurrentSector=SectorId.Hub;Call(visibility,"LateUpdate");Check(!speaker.mute,"unmute audible on reunion");
        service.ToggleMute(aPlayer);SectorTravelService.I.CurrentSector=SectorId.Containment;Call(visibility,"LateUpdate");
        SectorTravelService.I.CurrentSector=SectorId.Hub;Call(visibility,"LateUpdate");Check(speaker.mute,"travel cannot erase explicit mute");
        var replacement=new GameObject().AddComponent<AudioSource>();voice.SpeakerInUse=replacement;Call(visibility,"LateUpdate");
        Check(replacement.mute&&!speaker.mute,"replacement speaker muted and old restored");
        Call(visibility,"OnDisable");Check(!replacement.mute,"disable restores original source state");
        replacement.mute=true;Call(visibility,"Awake");service.ToggleMute(aPlayer);Call(visibility,"LateUpdate");
        Check(replacement.mute,"preexisting mute preserved");
        // Exercise the real board controller and local-hand trigger filter.
        for (int i=2;i<=10;i++) PhotonNetwork.CurrentRoom.Players[i]=Person(i,(100+i).ToString("X"));
        var board=Board();
        Call(board,"Refresh");Check(board.rows[0].player.IsLocal&&board.rows[4].player.ActorNumber==5,"stable first roster page");
        Check(board.pageText.text=="PAGE 1 / 2"&&board.heading.text.Contains("10 IN ROOM"),"ten players paginated");
        board.Press(PlayerBoardAction.Next,0);Check(board.rows[0].player.ActorNumber==6&&board.rows[4].player.ActorNumber==10,"second roster page");
        board.Press(PlayerBoardAction.Next,0);Check(board.pageText.text=="PAGE 2 / 2","pagination clamps");
        board.Press(PlayerBoardAction.Report,0);Check(board.reportPanel.activeSelf&&!board.rosterPanel.activeSelf,"report opens separate form");
        board.Press(PlayerBoardAction.Submit,0);Check(PlayFabClientAPI.Calls==calls,"no report without reason");
        board.Press(PlayerBoardAction.Reason,1);Check(board.reasons[1].label.text.StartsWith("[X]"),"reason selection feedback");
        board.Press(PlayerBoardAction.Cancel,0);Check(board.rosterPanel.activeSelf&&!board.reportPanel.activeSelf,"cancel sends nothing");
        Check(PlayFabClientAPI.Calls==calls,"cancel has no service call");
        var muteButton=board.rows[0].mute;Call(muteButton,"OnEnable");Tick(90);
        var remoteHand=new GameObject {layer=28};var remoteCollider=remoteHand.AddComponent<BoxCollider>();
        Call(muteButton,"OnTriggerEnter",remoteCollider);Check(!PlayerSafetyService.IsMuted(board.rows[0].player),"remote hand ignored");
        var localBody=new GameObject {layer=0};localBody.AddComponent<LocalRigMarker>();
        Call(muteButton,"OnTriggerEnter",localBody.AddComponent<BoxCollider>());Check(!PlayerSafetyService.IsMuted(board.rows[0].player),"local body ignored");
        var localHand=new GameObject {layer=28};localHand.AddComponent<LocalRigMarker>();var hand=localHand.AddComponent<BoxCollider>();
        Call(muteButton,"OnTriggerEnter",hand);Check(PlayerSafetyService.IsMuted(board.rows[0].player),"local fingertip mutes selected actor");
        Tick(92);Call(muteButton,"OnTriggerEnter",hand);Check(PlayerSafetyService.IsMuted(board.rows[0].player),"held contact cannot retrigger");
        Call(muteButton,"OnTriggerExit",hand);Call(muteButton,"OnTriggerEnter",hand);Check(!PlayerSafetyService.IsMuted(board.rows[0].player),"release and repress unmutes");
        board.Press(PlayerBoardAction.Report,0);PhotonNetwork.CurrentRoom=new Room();service.OnJoinedRoom();Call(board,"Refresh");
        Check(board.rosterPanel.activeSelf&&!board.reportPanel.activeSelf&&board.heading.text.Contains("0 IN ROOM"),"room change cancels stale report form");
        TestReportFeedback(service);
        TestLateReportConfirmation(service);
        Console.WriteLine("PASS: "+assertions+" social-safety assertions; production service, state, voice integration compiled with diagnostic doubles.");
    }
    static TMPro.TMP_Text Label()=>new GameObject().AddComponent<TMPro.TMP_Text>();
    static PlayerBoard Board()
    {
        var board=new GameObject().AddComponent<PlayerBoard>();
        board.heading=Label();board.status=Label();board.pageText=Label();board.reportHeading=Label();board.reportHelp=Label();
        board.rosterPanel=new GameObject();board.reportPanel=new GameObject();
        board.previous=Button(board,PlayerBoardAction.Previous);board.next=Button(board,PlayerBoardAction.Next);
        board.close=Button(board,PlayerBoardAction.Close);board.submit=Button(board,PlayerBoardAction.Submit);
        board.reasons=new PlayerBoardButton[5];board.rows=new PlayerBoard.Row[5];
        for(int i=0;i<5;i++)
        {
            board.reasons[i]=Button(board,PlayerBoardAction.Reason,i);
            board.rows[i]=new PlayerBoard.Row {root=new GameObject(),nameText=Label(),detailText=Label(),mute=Button(board,PlayerBoardAction.Mute,i),report=Button(board,PlayerBoardAction.Report,i)};
        }
        return board;
    }
    static void TestReportFeedback(PlayerSafetyService service)
    {
        PhotonNetwork.CurrentRoom=new Room();
        PhotonNetwork.CurrentRoom.Players[1]=PhotonNetwork.LocalPlayer;
        for(int i=2;i<=4;i++) PhotonNetwork.CurrentRoom.Players[i]=Person(i,(100+i).ToString("X"));
        service.OnJoinedRoom();Tick(100);
        var hub=Board();var portable=Board();portable.portable=true;
        Call(hub,"Refresh");Call(portable,"Refresh");
        hub.Press(PlayerBoardAction.Report,1);hub.Press(PlayerBoardAction.Reason,0);hub.Press(PlayerBoardAction.Submit,0);
        PlayFabClientAPI.Success(new ReportPlayerClientResult {SubmissionsRemaining=4});Call(hub,"Refresh");
        Check(hub.status.text=="Report submitted. Thank you.","submitted form shows its receipt");
        int calls=PlayFabClientAPI.Calls;
        hub.Press(PlayerBoardAction.Cancel,0);hub.Press(PlayerBoardAction.Report,2);
        Check(hub.reportHeading.text.Contains("#3")&&!service.State.WasReported("67")&&PlayFabClientAPI.Calls==calls,"new target has not been submitted");
        Check(hub.status.text=="Choose a reason, then SEND REPORT.","switching players clears previous player's receipt");

        portable.Press(PlayerBoardAction.Report,3);portable.Press(PlayerBoardAction.Reason,3);
        Tick(110);hub.Press(PlayerBoardAction.Reason,1);hub.Press(PlayerBoardAction.Submit,0);Call(portable,"Refresh");
        Check(hub.submit.label.text=="SENDING..."&&hub.status.text=="Sending report...","request owner shows sending");
        Check(portable.submit.label.text=="WAIT..."&&portable.status.text=="Another report is sending. Please wait.","other board waits without claiming its own submission");
        hub.Press(PlayerBoardAction.Cancel,0);
        PlayFabClientAPI.Success(new ReportPlayerClientResult {SubmissionsRemaining=3});Call(portable,"Refresh");Call(hub,"Refresh");
        Check(portable.status.text=="Choose a reason, then SEND REPORT."&&!service.State.WasReported("68"),"late receipt cannot attach to the other board's target");
        Check(hub.status.text=="Mute only affects what you hear.","roster does not show an unqualified report receipt");
        hub.Press(PlayerBoardAction.Report,2);
        Check(hub.status.text=="Report submitted. Thank you."&&hub.submit.label.text=="SUBMITTED","reopening a confirmed target shows its room receipt");

        Tick(120);portable.Press(PlayerBoardAction.Submit,0);
        service.ToggleMute(PhotonNetwork.CurrentRoom.GetPlayer(2));Call(portable,"Refresh");
        Check(portable.status.text=="Sending report...","mute feedback cannot overwrite pending report feedback");
        PlayFabClientAPI.Failure(new PlayFabError());Call(portable,"Refresh");Call(hub,"Refresh");
        Check(portable.status.text.Contains("could not be sent"),"failure appears on originating form");
        Check(hub.status.text=="Report submitted. Thank you.","another target's failure cannot overwrite a confirmed receipt");
        portable.Press(PlayerBoardAction.Close,0);portable.gameObject.SetActive(true);Call(portable,"OnEnable");Call(portable,"Refresh");
        Check(portable.reportHeading.text.Contains("#4")&&portable.status.text.Contains("could not be sent"),"reopened portable form retains only its own feedback");

        Tick(124);portable.Press(PlayerBoardAction.Submit,0);var late=PlayFabClientAPI.Success;
        Tick(145);Call(service,"Update");Call(portable,"Refresh");
        Check(portable.status.text.Contains("No confirmation"),"timeout stays with originating form");
        portable.Press(PlayerBoardAction.Submit,0);late(new ReportPlayerClientResult {SubmissionsRemaining=2});Call(portable,"Refresh");
        Check(portable.status.text=="Sending report..."&&service.State.IsSending,"stale callback cannot overwrite retry status");
        var old=PlayFabClientAPI.Success;PhotonNetwork.CurrentRoom=new Room();service.OnJoinedRoom();old(new ReportPlayerClientResult {SubmissionsRemaining=2});
        Call(hub,"Refresh");Call(portable,"Refresh");
        Check(hub.rosterPanel.activeSelf&&portable.rosterPanel.activeSelf&&hub.status.text=="Mute only affects what you hear."&&portable.status.text==hub.status.text,"room change clears both forms and late feedback");
    }
    static void TestLateReportConfirmation(PlayerSafetyService service)
    {
        PhotonNetwork.CurrentRoom=new Room();
        PhotonNetwork.CurrentRoom.Players[1]=PhotonNetwork.LocalPlayer;
        PhotonNetwork.CurrentRoom.Players[2]=Person(2,"ABC");
        service.OnJoinedRoom();Tick(200);
        var board=Board();Call(board,"Refresh");
        board.Press(PlayerBoardAction.Report,1);board.Press(PlayerBoardAction.Reason,2);board.Press(PlayerBoardAction.Submit,0);
        int calls=PlayFabClientAPI.Calls;
        Tick(221);Call(service,"Update");Call(board,"Refresh");
        Check(!service.State.IsSending&&board.status.text.Contains("No confirmation"),"deadline unlocks the form while delivery is unknown");
        PlayFabClientAPI.Success(new ReportPlayerClientResult {SubmissionsRemaining=4});Call(board,"Refresh");
        Check(service.State.WasReported("ABC")&&board.status.text=="Report submitted. Thank you."&&board.submit.label.text=="SUBMITTED","late confirmation resolves an expired request when no retry superseded it");
        board.Press(PlayerBoardAction.Submit,0);
        Check(PlayFabClientAPI.Calls==calls,"late confirmed delivery suppresses duplicate submission");
        PlayFabClientAPI.Failure(new PlayFabError());Call(board,"Refresh");
        Check(board.status.text=="Report submitted. Thank you.","duplicate callback cannot revoke confirmed delivery");
    }
    static void TestHubPresenter()
    {
        var presenter=new GameObject().AddComponent<PlayerBoardHubPresenter>();
        var anchor=new GameObject("SpawnRoom");anchor.SetActive(false);
        var hub=new Scene {name="Hub_Base",isLoaded=true,roots=new[]{anchor}};
        Resources.Assets["SocialSafety/PlayerBoardHub"]=new GameObject("PlayerBoardHub");
        Call(presenter,"OnEnable");
        SceneManager.Loaded(new Scene {name="Level1_Containment",isLoaded=true,roots=new[]{anchor}});
        Check(UnityEngine.Object.ParentInstantiations==0,"no Hub board in other sectors");
        SceneManager.Loaded(hub);
        Check(UnityEngine.Object.ParentInstantiations==1&&UnityEngine.Object.LastInstance.transform.parent==anchor.transform&&!anchor.activeSelf,"board attaches to hidden room without revealing it");
        SceneManager.Loaded(hub);
        Check(UnityEngine.Object.ParentInstantiations==1,"duplicate scene notification does not duplicate board");
        Call(presenter,"OnDisable");
        var nextAnchor=new GameObject("SpawnRoom");hub.roots=new[]{nextAnchor};
        SceneManager.Loaded(hub);Check(UnityEngine.Object.ParentInstantiations==1,"disabled presenter unsubscribes");
        SceneManager.Scenes.Add(hub);Call(presenter,"OnEnable");
        Check(UnityEngine.Object.ParentInstantiations==2&&UnityEngine.Object.LastInstance.transform.parent==nextAnchor.transform,"enable discovers an already-loaded or revisited Hub");
        hub.roots=new[]{new GameObject("WrongRoot")};SceneManager.Loaded(hub);
        Check(UnityEngine.Object.ParentInstantiations==2,"missing anchor fails closed");
        hub.roots=new[]{new GameObject("SpawnRoom")};hub.isLoaded=false;SceneManager.Loaded(hub);
        Check(UnityEngine.Object.ParentInstantiations==2,"unloaded scene ignored");hub.isLoaded=true;
        var prefab=Resources.Assets["SocialSafety/PlayerBoardHub"];Resources.Assets.Clear();SceneManager.Loaded(hub);
        Check(UnityEngine.Object.ParentInstantiations==2,"missing prefab fails closed");Resources.Assets["SocialSafety/PlayerBoardHub"]=prefab;
        var manual=new GameObject();manual.AddComponent<PlayerBoard>();manual.SetActive(false);
        hub.roots=new[]{hub.roots[0],manual};SceneManager.Loaded(hub);
        Check(UnityEngine.Object.ParentInstantiations==2,"inactive manually authored board prevents fallback");
        manual.GetComponent<PlayerBoard>().portable=true;SceneManager.Loaded(hub);
        Check(UnityEngine.Object.ParentInstantiations==3,"portable board does not replace Hub installation");
        Call(presenter,"OnDisable");SceneManager.Scenes.Clear();Resources.Assets.Clear();
    }
    static PlayerBoardButton Button(PlayerBoard board,PlayerBoardAction action,int index=0)
    {
        var button=new GameObject().AddComponent<PlayerBoardButton>();button.board=board;button.action=action;button.index=index;button.label=Label();return button;
    }
}
