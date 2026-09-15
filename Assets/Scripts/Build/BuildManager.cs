using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Defense2D
{
    /// <summary>
    /// TAB으로 건설 메뉴를 열고, 마우스로 타워를 배치한다. (기획서 02 GAMEPLAY 조작안)
    /// 경로 위/근처와 다른 타워와 너무 가까운 곳에는 배치할 수 없다.
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        public PathData Path;
        public GameManager Game;
        public UIManager UI;
        public Camera Cam;

        public bool MenuOpen { get; private set; }

        private TowerType? _selected;
        private readonly List<GameObject> _towers = new List<GameObject>();
        private GameObject _ghost;
        private SpriteRenderer _ghostSr;
        private GameObject _rangeGhost;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame)
            {
                MenuOpen = !MenuOpen;
                if (!MenuOpen) CancelSelection();
                UI.SetBuildMenuOpen(MenuOpen);
            }

            if (kb.escapeKey.wasPressedThisFrame) CancelSelection();

            if (_selected.HasValue)
            {
                UpdateGhost();
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                    TryPlace();
                if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                    CancelSelection();
            }
        }

        private bool IsPointerOverUI() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public void SelectTower(TowerType type)
        {
            _selected = type;
            EnsureGhost();
        }

        private void CancelSelection()
        {
            _selected = null;
            if (_ghost != null) Destroy(_ghost);
            if (_rangeGhost != null) Destroy(_rangeGhost);
        }

        private void EnsureGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            if (_rangeGhost != null) Destroy(_rangeGhost);

            _ghost = new GameObject("PlaceGhost");
            _ghostSr = _ghost.AddComponent<SpriteRenderer>();
            _ghostSr.sprite = SpriteFactory.Triangle(new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0.8f));
            _ghostSr.sortingOrder = 20;

            _rangeGhost = new GameObject("RangeGhost");
            var rsr = _rangeGhost.AddComponent<SpriteRenderer>();
            rsr.sprite = SpriteFactory.Ring(new Color(1, 1, 1, 0.35f));
            rsr.sortingOrder = 19;
        }

        private Vector3 MouseWorld()
        {
            var mouse = Mouse.current;
            if (mouse == null || Cam == null) return Vector3.zero;
            Vector3 sp = mouse.position.ReadValue();
            sp.z = -Cam.transform.position.z;
            Vector3 world = Cam.ScreenToWorldPoint(sp);
            world.z = 0f;
            return world;
        }

        private void UpdateGhost()
        {
            Vector3 pos = MouseWorld();
            _ghost.transform.position = pos;
            _rangeGhost.transform.position = pos;

            float range = TowerRangeFor(_selected.Value);
            _rangeGhost.transform.localScale = Vector3.one * (range * 2f / 3f); // Ring 스프라이트 지름 3유닛 기준 보정

            bool valid = IsValidPlacement(pos);
            _ghostSr.color = valid ? new Color(1, 1, 1, 0.55f) : new Color(1, 0.3f, 0.3f, 0.55f);
        }

        private float TowerRangeFor(TowerType t) => t switch
        {
            TowerType.Arrow => 3.2f,
            TowerType.Ice => 2.6f,
            TowerType.Cannon => 2.9f,
            _ => 2.5f
        };

        private bool IsValidPlacement(Vector3 pos)
        {
            if (Mathf.Abs(pos.x) > GameConstants.WorldHalfWidth - 0.3f) return false;
            if (Mathf.Abs(pos.y) > GameConstants.WorldHalfHeight - 0.3f) return false;
            if (Path.DistanceToNearestPath(pos) < GameConstants.MinDistanceFromPath) return false;

            foreach (var t in _towers)
            {
                if (t == null) continue;
                if (Vector2.Distance(t.transform.position, pos) < GameConstants.MinDistanceBetweenTowers) return false;
            }
            return true;
        }

        private void TryPlace()
        {
            Vector3 pos = MouseWorld();
            if (!IsValidPlacement(pos)) return;
            if (Game.Gold < GameConstants.TowerCost)
            {
                Game.ShowBanner("골드가 부족합니다");
                return;
            }

            Game.SpendGold(GameConstants.TowerCost);
            SpawnTower(_selected.Value, pos);
        }

        private void SpawnTower(TowerType type, Vector3 pos)
        {
            var go = new GameObject($"Tower_{type}");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 4;

            Color fill = type switch
            {
                TowerType.Arrow => new Color(0.24f, 0.4f, 0.78f),
                TowerType.Ice => new Color(0.35f, 0.85f, 0.92f),
                TowerType.Cannon => new Color(0.95f, 0.58f, 0.28f),
                _ => Color.white
            };
            sr.sprite = SpriteFactory.Triangle(fill, Color.white);

            switch (type)
            {
                case TowerType.Arrow: go.AddComponent<ArrowTower>().Setup(); break;
                case TowerType.Ice: go.AddComponent<IceTower>().Setup(); break;
                case TowerType.Cannon: go.AddComponent<CannonTower>().Setup(); break;
            }

            _towers.Add(go);
        }

        /// <summary>보스 능력(2번, 5번 페이즈)으로 임의의 타워를 잠시 무력화한다.</summary>
        public void DisableRandomTowerBriefly(float seconds)
        {
            _towers.RemoveAll(t => t == null);
            if (_towers.Count == 0) return;
            var t = _towers[Random.Range(0, _towers.Count)];
            var tb = t.GetComponent<TowerBase>();
            tb?.SetDisabledFor(seconds);
        }
    }
}
