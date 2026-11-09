#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PVZ.Player;
using PVZ.Interactions;
using PVZ.Items;
using PVZ.Warehouse;
using PVZ.Economy;
using PVZ.Computer;
using PVZ.Computer.UI;
using PVZ.Delivery;
using PVZ.Customers;
using PVZ.Counter;
using PVZ.Calendar;
using PVZ.Calendar.UI;

namespace PVZ.Editor
{
    /// <summary>
    /// Builds and populates the TestRoom scene for PVZ Simulator:
    /// - One single entrance on the front (South) wall.
    /// - Outdoor street area outside the entrance (empty until delivery arrives).
    /// - Inside: customer lobby, service counter with walk-through opening, PC desk, and warehouse shelves.
    /// - Outdoor delivery truck arrives on the street right outside the front entrance and leaves when empty.
    /// </summary>
    public static class TestSceneBuilder
    {
        private const string SceneDirectory = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/TestRoom.unity";

        [MenuItem("Tools/PVZ/Rebuild Test Room Scene")]
        public static void BuildScene()
        {
            if (!Directory.Exists(SceneDirectory))
            {
                Directory.CreateDirectory(SceneDirectory);
            }

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Managers
            GameObject managersObj = new GameObject("--- MANAGERS ---");

            GameObject wmObj = new GameObject("WarehouseManager");
            wmObj.transform.SetParent(managersObj.transform);
            wmObj.AddComponent<WarehouseManager>();

            GameObject emObj = new GameObject("EconomyManager");
            emObj.transform.SetParent(managersObj.transform);
            emObj.AddComponent<EconomyManager>();

            GameObject rmObj = new GameObject("RatingManager");
            rmObj.transform.SetParent(managersObj.transform);
            rmObj.AddComponent<RatingManager>();

            GameObject dmObj = new GameObject("DeliveryManager");
            dmObj.transform.SetParent(managersObj.transform);
            var deliveryMgr = dmObj.AddComponent<DeliveryManager>();

            GameObject qmObj = new GameObject("CustomerQueueManager");
            qmObj.transform.SetParent(managersObj.transform);
            qmObj.AddComponent<CustomerQueueManager>();

            GameObject cmObj = new GameObject("CalendarManager");
            cmObj.transform.SetParent(managersObj.transform);
            cmObj.AddComponent<CalendarManager>();

            // 2. Lighting
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.2f;
            dirLight.color = new Color(1f, 0.98f, 0.92f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 3. Indoor Floor (Z = -5.0 to 12.0, X = -7.5 to 7.5) -> size 15x17m
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Indoor_Floor";
            floor.transform.position = new Vector3(0f, 0f, 3.5f);
            floor.transform.localScale = new Vector3(1.5f, 1f, 1.7f);

            // 4. Outdoor Street / Sidewalk Floor outside front entrance (Z = -5.0 to -22.0)
            GameObject streetFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            streetFloor.name = "Outdoor_Street_Floor";
            streetFloor.transform.position = new Vector3(0f, -0.01f, -13.5f);
            streetFloor.transform.localScale = new Vector3(2.5f, 1f, 1.7f);

            Renderer streetRend = streetFloor.GetComponent<Renderer>();
            if (streetRend != null && streetRend.sharedMaterial != null)
            {
                Material mat = new Material(streetRend.sharedMaterial) { color = new Color(0.28f, 0.28f, 0.30f) };
                streetRend.sharedMaterial = mat;
            }

            // 5. Room Walls
            // North Wall (SOLID back wall — no rear doors!)
            CreateWall("Wall_North", new Vector3(0f, 2f, 12.0f), new Vector3(15.4f, 4f, 0.4f));

            // South Wall (Front wall with the single entrance doorway)
            CreateWall("Wall_South_Left", new Vector3(-4.6f, 2f, -5.0f), new Vector3(5.8f, 4f, 0.4f));
            CreateWall("Wall_South_Right", new Vector3(4.6f, 2f, -5.0f), new Vector3(5.8f, 4f, 0.4f));
            CreateWall("Wall_South_Arch", new Vector3(0f, 3.5f, -5.0f), new Vector3(3.4f, 1f, 0.4f));

            // West & East Walls
            CreateWall("Wall_West", new Vector3(-7.5f, 2f, 3.5f), new Vector3(0.4f, 4f, 17.4f));
            CreateWall("Wall_East", new Vector3(7.5f, 2f, 3.5f), new Vector3(0.4f, 4f, 17.4f));

            // 6. Checkout Counter Desk (at Z = 0.5f, with open passage on the right)
            GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "CounterTable";
            counter.transform.position = new Vector3(-1.8f, 0.45f, 0.5f);
            counter.transform.localScale = new Vector3(4.2f, 0.9f, 0.8f);

            var checkoutCounter = counter.AddComponent<CheckoutCounter>();

            // Open/Closed Sign Tablet (tilted towards employee side Z > 0.5)
            GameObject signTablet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            signTablet.name = "Sign_OpenClosed";
            signTablet.transform.SetParent(counter.transform);
            signTablet.transform.localPosition = new Vector3(-1.1f, 0.55f, 0f);
            signTablet.transform.localRotation = Quaternion.Euler(20f, 180f, 0f);
            signTablet.transform.localScale = new Vector3(0.5f, 0.15f, 0.35f);

            Renderer signRend = signTablet.GetComponent<Renderer>();
            if (signRend != null && signRend.sharedMaterial != null)
            {
                Material mat = new Material(signRend.sharedMaterial) { color = new Color(0.85f, 0.15f, 0.15f) };
                signRend.sharedMaterial = mat;
            }

            // Scanner Stand / Cradle
            GameObject scannerStand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            scannerStand.name = "Scanner_Stand";
            scannerStand.transform.SetParent(counter.transform);
            scannerStand.transform.localPosition = new Vector3(0.8f, 0.52f, 0f);
            scannerStand.transform.localScale = new Vector3(0.18f, 0.04f, 0.18f);

            Renderer standRend = scannerStand.GetComponent<Renderer>();
            if (standRend != null && standRend.sharedMaterial != null)
            {
                standRend.sharedMaterial = new Material(standRend.sharedMaterial) { color = new Color(0.15f, 0.15f, 0.18f) };
            }

            // Handheld Barcode Scanner Item
            GameObject scannerGo = new GameObject("Handheld_Barcode_Scanner");
            scannerGo.transform.position = counter.transform.position + new Vector3(0.8f, 1.05f, 0f);
            scannerGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            BarcodeScannerItem scannerItem = scannerGo.AddComponent<BarcodeScannerItem>();
            scannerItem.CradleTransform = scannerStand.transform;

            // Wire fields of CheckoutCounter
            SerializedObject sCounter = new SerializedObject(checkoutCounter);
            sCounter.FindProperty("_statusSignTablet").objectReferenceValue = signTablet.transform;
            sCounter.FindProperty("_barcodeScanner").objectReferenceValue = scannerStand.transform;
            sCounter.FindProperty("_scannerItem").objectReferenceValue = scannerItem;
            sCounter.FindProperty("_statusSignRenderer").objectReferenceValue = signRend;
            sCounter.ApplyModifiedProperties();

            // 7. Computer Desk (behind counter in employee area)
            CreateComputerWorkstation(new Vector3(2.5f, 0f, 2.5f), Quaternion.Euler(0f, 180f, 0f));

            // 8. Warehouse Shelves (in employee warehouse area)
            CreateShelfUnit("Shelf_A", "A", new Vector3(-6.2f, 0f, 4.0f), Quaternion.Euler(0f, 90f, 0f));
            CreateShelfUnit("Shelf_B", "B", new Vector3(-6.2f, 0f, 8.5f), Quaternion.Euler(0f, 90f, 0f));

            // 8.1. Wall Calendar (mounted on warehouse wall)
            CreateWallCalendar(new Vector3(-4.0f, 1.8f, 11.78f), Quaternion.Euler(0f, 180f, 0f));

            // 9. Delivery Truck (parked on the street outside the front entrance)
            DeliveryTruck truck = CreateDeliveryTruck(new Vector3(-2.2f, 0f, -11.0f), Quaternion.Euler(0f, 180f, 0f));
            truck.SetParkTransform(new Vector3(-2.2f, 0f, -11.0f), Quaternion.Euler(0f, 180f, 0f));

            // Truck is initially inactive so the street is empty!
            truck.gameObject.SetActive(false);

            // Wire truck to DeliveryManager
            SerializedObject sDM = new SerializedObject(deliveryMgr);
            sDM.FindProperty("_truck").objectReferenceValue = truck;
            sDM.ApplyModifiedProperties();

            // 10. Player Character (spawns behind the counter in employee area)
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1.0f, 1.8f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Facing South towards counter & front entrance

            var defaultCapsule = player.GetComponent<CapsuleCollider>();
            if (defaultCapsule != null)
            {
                Object.DestroyImmediate(defaultCapsule);
            }

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.center = Vector3.zero;

            // First Person Camera
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            cameraObj.transform.SetParent(player.transform);
            cameraObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            cameraObj.transform.localRotation = Quaternion.identity;

            Camera cam = cameraObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cameraObj.AddComponent<AudioListener>();

            // Hold Point
            GameObject holdPoint = new GameObject("HoldPoint");
            holdPoint.transform.SetParent(cameraObj.transform);
            holdPoint.transform.localPosition = new Vector3(0.25f, -0.25f, 0.9f);
            holdPoint.transform.localRotation = Quaternion.identity;

            // Attach Controllers
            PlayerController playerCtrl = player.AddComponent<PlayerController>();
            PickupController pickupCtrl = player.AddComponent<PickupController>();

            SerializedObject sPlayer = new SerializedObject(playerCtrl);
            sPlayer.FindProperty("_cameraTransform").objectReferenceValue = cameraObj.transform;
            sPlayer.ApplyModifiedProperties();

            SerializedObject sPickup = new SerializedObject(pickupCtrl);
            sPickup.FindProperty("_playerCamera").objectReferenceValue = cam;
            sPickup.FindProperty("_holdPoint").objectReferenceValue = holdPoint.transform;
            sPickup.ApplyModifiedProperties();

            // 11. Initial Starter Boxes (Disabled per economy design: starts with 0 boxes on shelves)
            // Player must order their first free delivery from the computer terminal.

            // Save Scene
            EditorSceneManager.SaveScene(newScene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Debug.Log("<color=green>[PVZ Simulator]</color> Сцена успешно пересобрана!");
        }

        private static void CreateWall(string name, Vector3 pos, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
        }

        private static DeliveryTruck CreateDeliveryTruck(Vector3 position, Quaternion rotation)
        {
            GameObject truckObj = new GameObject("DeliveryTruck");
            truckObj.transform.position = position;
            truckObj.transform.rotation = rotation;

            DeliveryTruck truck = truckObj.AddComponent<DeliveryTruck>();

            // Cabin (front of truck)
            CreatePart(truckObj.transform, "Cab", new Vector3(0f, 1.25f, 3.2f), new Vector3(2.3f, 2.1f, 2.2f));
            CreatePart(truckObj.transform, "Bumper", new Vector3(0f, 0.35f, 4.35f), new Vector3(2.4f, 0.4f, 0.3f));

            // Cargo Body
            CreatePart(truckObj.transform, "Cargo_Floor", new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 0.1f, 4.4f));
            CreatePart(truckObj.transform, "Cargo_LeftWall", new Vector3(-1.15f, 1.6f, 0f), new Vector3(0.1f, 2.1f, 4.4f));
            CreatePart(truckObj.transform, "Cargo_RightWall", new Vector3(1.15f, 1.6f, 0f), new Vector3(0.1f, 2.1f, 4.4f));
            CreatePart(truckObj.transform, "Cargo_FrontWall", new Vector3(0f, 1.6f, 2.15f), new Vector3(2.2f, 2.1f, 0.1f));
            CreatePart(truckObj.transform, "Cargo_Roof", new Vector3(0f, 2.65f, 0f), new Vector3(2.4f, 0.1f, 4.4f));

            // Cargo Bay Transform socket for spawning boxes inside
            GameObject cargoBay = new GameObject("CargoBay");
            cargoBay.transform.SetParent(truckObj.transform);
            cargoBay.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            cargoBay.transform.localRotation = Quaternion.identity;

            // Rear Doors (attached to hinges at back z = -2.2)
            GameObject leftDoorHinge = new GameObject("LeftDoorHinge");
            leftDoorHinge.transform.SetParent(truckObj.transform);
            leftDoorHinge.transform.localPosition = new Vector3(-1.15f, 1.6f, -2.2f);
            leftDoorHinge.transform.localRotation = Quaternion.identity;

            GameObject leftDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftDoor.name = "LeftDoor";
            leftDoor.transform.SetParent(leftDoorHinge.transform);
            leftDoor.transform.localPosition = new Vector3(0.55f, 0f, 0f);
            leftDoor.transform.localRotation = Quaternion.identity;
            leftDoor.transform.localScale = new Vector3(1.1f, 2.0f, 0.08f);

            GameObject rightDoorHinge = new GameObject("RightDoorHinge");
            rightDoorHinge.transform.SetParent(truckObj.transform);
            rightDoorHinge.transform.localPosition = new Vector3(1.15f, 1.6f, -2.2f);
            rightDoorHinge.transform.localRotation = Quaternion.identity;

            GameObject rightDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightDoor.name = "RightDoor";
            rightDoor.transform.SetParent(rightDoorHinge.transform);
            rightDoor.transform.localPosition = new Vector3(-0.55f, 0f, 0f);
            rightDoor.transform.localRotation = Quaternion.identity;
            rightDoor.transform.localScale = new Vector3(1.1f, 2.0f, 0.08f);

            // Extendable Ramp at the rear of cargo floor (Z = -2.2, Y = 0.45)
            GameObject rampHinge = new GameObject("RampHinge");
            rampHinge.transform.SetParent(truckObj.transform);
            rampHinge.transform.localPosition = new Vector3(0f, 0.45f, -2.2f);
            rampHinge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Initially folded up against doors

            GameObject rampPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rampPlate.name = "RampPlate";
            rampPlate.transform.SetParent(rampHinge.transform);
            rampPlate.transform.localPosition = new Vector3(0f, 0f, -0.65f);
            rampPlate.transform.localRotation = Quaternion.identity;
            rampPlate.transform.localScale = new Vector3(1.5f, 0.06f, 1.3f);

            Renderer rampRend = rampPlate.GetComponent<Renderer>();
            if (rampRend != null && rampRend.sharedMaterial != null)
            {
                Material mat = new Material(rampRend.sharedMaterial) { color = new Color(0.6f, 0.6f, 0.65f) };
                rampRend.sharedMaterial = mat;
            }

            // Wire truck properties
            SerializedObject sTruck = new SerializedObject(truck);
            sTruck.FindProperty("_cargoBay").objectReferenceValue = cargoBay.transform;
            sTruck.FindProperty("_leftDoor").objectReferenceValue = leftDoorHinge.transform;
            sTruck.FindProperty("_rightDoor").objectReferenceValue = rightDoorHinge.transform;
            sTruck.FindProperty("_ramp").objectReferenceValue = rampHinge.transform;
            sTruck.ApplyModifiedProperties();

            return truck;
        }

        private static void CreateComputerWorkstation(Vector3 position, Quaternion rotation)
        {
            GameObject deskObj = new GameObject("ComputerWorkstation");
            deskObj.transform.position = position;
            deskObj.transform.rotation = rotation;

            CreatePart(deskObj.transform, "Desk_Top", new Vector3(0f, 0.72f, 0f), new Vector3(1.6f, 0.05f, 0.9f));
            CreatePart(deskObj.transform, "Desk_Leg_L", new Vector3(-0.7f, 0.35f, 0f), new Vector3(0.08f, 0.7f, 0.7f));
            CreatePart(deskObj.transform, "Desk_Leg_R", new Vector3(0.7f, 0.35f, 0f), new Vector3(0.08f, 0.7f, 0.7f));

            GameObject monitorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monitorObj.name = "PC_Monitor_Terminal";
            monitorObj.transform.SetParent(deskObj.transform);
            monitorObj.transform.localPosition = new Vector3(0f, 1.05f, 0.15f);
            monitorObj.transform.localRotation = Quaternion.identity;
            monitorObj.transform.localScale = new Vector3(0.8f, 0.5f, 0.08f);

            var terminal = monitorObj.AddComponent<ComputerTerminal>();
            monitorObj.AddComponent<ComputerUI>();

            CreatePart(deskObj.transform, "Monitor_Stand", new Vector3(0f, 0.77f, 0.15f), new Vector3(0.2f, 0.06f, 0.2f));
            CreatePart(deskObj.transform, "Keyboard", new Vector3(0f, 0.76f, -0.15f), new Vector3(0.45f, 0.02f, 0.16f));
            CreatePart(deskObj.transform, "PC_Tower", new Vector3(0.6f, 0.95f, 0.1f), new Vector3(0.2f, 0.42f, 0.45f));
        }

        private static void CreateShelfUnit(string shelfName, string letter, Vector3 position, Quaternion rotation)
        {
            GameObject shelfObj = new GameObject(shelfName);
            shelfObj.transform.position = position;
            shelfObj.transform.rotation = rotation;

            var shelfComp = shelfObj.AddComponent<WarehouseShelf>();

            CreatePart(shelfObj.transform, "Frame_Left", new Vector3(-1.6f, 1.5f, 0f), new Vector3(0.1f, 3f, 0.8f));
            CreatePart(shelfObj.transform, "Frame_Right", new Vector3(1.6f, 1.5f, 0f), new Vector3(0.1f, 3f, 0.8f));
            CreatePart(shelfObj.transform, "Frame_Back", new Vector3(0f, 1.5f, -0.4f), new Vector3(3.3f, 3f, 0.05f));

            CreatePart(shelfObj.transform, "Plank_Bottom", new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 0.08f, 0.8f));
            CreatePart(shelfObj.transform, "Plank_Top", new Vector3(0f, 1.8f, 0f), new Vector3(3.2f, 0.08f, 0.8f));

            CreateSlot(shelfObj.transform, $"{letter}1", new Vector3(-0.8f, 0.54f, 0f));
            CreateSlot(shelfObj.transform, $"{letter}2", new Vector3(0.8f, 0.54f, 0f));
            CreateSlot(shelfObj.transform, $"{letter}3", new Vector3(-0.8f, 1.84f, 0f));
            CreateSlot(shelfObj.transform, $"{letter}4", new Vector3(0.8f, 1.84f, 0f));

            shelfComp.ConfigureSlots(letter);
        }

        private static GameObject CreatePart(Transform parent, string name, Vector3 localPos, Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent);
            part.transform.localPosition = localPos;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            return part;
        }

        private static void CreateWallCalendar(Vector3 position, Quaternion rotation)
        {
            GameObject calendarObj = new GameObject("WallCalendar");
            calendarObj.transform.position = position;
            calendarObj.transform.rotation = rotation;

            // Cork/Wood backing board
            GameObject board = CreatePart(calendarObj.transform, "Backboard", new Vector3(0f, 0f, 0.02f), new Vector3(0.85f, 1.05f, 0.03f));
            Renderer boardRend = board.GetComponent<Renderer>();
            if (boardRend != null && boardRend.sharedMaterial != null)
            {
                Material mat = new Material(boardRend.sharedMaterial) { color = new Color(0.35f, 0.22f, 0.12f) };
                boardRend.sharedMaterial = mat;
            }

            // White Calendar Paper sheet
            GameObject paper = CreatePart(calendarObj.transform, "PaperPage", new Vector3(0f, -0.04f, 0f), new Vector3(0.78f, 0.88f, 0.02f));
            Renderer paperRend = paper.GetComponent<Renderer>();
            if (paperRend != null && paperRend.sharedMaterial != null)
            {
                Material mat = new Material(paperRend.sharedMaterial) { color = new Color(0.96f, 0.96f, 0.94f) };
                paperRend.sharedMaterial = mat;
            }

            // Red top binding header
            GameObject topBinding = CreatePart(calendarObj.transform, "TopBinding", new Vector3(0f, 0.44f, -0.01f), new Vector3(0.80f, 0.12f, 0.03f));
            Renderer bindRend = topBinding.GetComponent<Renderer>();
            if (bindRend != null && bindRend.sharedMaterial != null)
            {
                Material mat = new Material(bindRend.sharedMaterial) { color = new Color(0.85f, 0.15f, 0.15f) };
                bindRend.sharedMaterial = mat;
            }

            // Collider for interaction raycasting
            BoxCollider col = calendarObj.AddComponent<BoxCollider>();
            col.size = new Vector3(0.85f, 1.05f, 0.1f);
            col.center = new Vector3(0f, 0f, 0f);

            calendarObj.AddComponent<WallCalendar>();
            calendarObj.AddComponent<CalendarUI>();
        }

        private static void CreateSlot(Transform parent, string slotId, Vector3 localPos)
        {
            GameObject slotObj = new GameObject($"Slot_{slotId}");
            slotObj.transform.SetParent(parent);
            slotObj.transform.localPosition = localPos;
            slotObj.transform.localRotation = Quaternion.identity;

            var boxCol = slotObj.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(1.1f, 0.8f, 0.8f);
            boxCol.center = new Vector3(0f, 0.4f, 0f);

            var slot = slotObj.AddComponent<WarehouseSlot>();
            slot.Initialize(slotId);

            GameObject snap = new GameObject("SnapPoint");
            snap.transform.SetParent(slotObj.transform);
            snap.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            snap.transform.localRotation = Quaternion.identity;

            SerializedObject sSlot = new SerializedObject(slot);
            sSlot.FindProperty("_snapPoint").objectReferenceValue = snap.transform;
            sSlot.ApplyModifiedProperties();
        }

        private static BoxItem CreateBox(string name, int orderId, Vector3 worldPos)
        {
            GameObject boxObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boxObj.name = name;
            boxObj.transform.position = worldPos;
            boxObj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

            Rigidbody rb = boxObj.AddComponent<Rigidbody>();
            rb.mass = 1.0f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            BoxItem boxItem = boxObj.AddComponent<BoxItem>();
            boxItem.Initialize(orderId);

            return boxItem;
        }
    }
}
#endif
