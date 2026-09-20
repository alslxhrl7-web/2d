using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Defense2D
{
    /// <summary>
    /// TAB으로 건설 메뉴를 열고, 마우스로 타워를 배치한다. (기획서 02 GAMEPLAY 조작안)
    /// 경로 위/근처와 다른 타워와 너무 가까운 곳에는 배치할 수 없다.
    ///
    /// [해설] ★ 타워 종류를 <b>플레이어가 직접 고른다</b>. 예전에는 설치 버튼 하나만 있고
    /// 미리보기가 네 타입을 빠르게 돌아가다가 클릭하는 순간의 타입으로 확정됐다 — 사실상
    /// 무작위였다. 디펜스 장르의 핵심 동사가 "고르고 놓는다"인데 "놓는다"만 남아 있었던
    /// 셈이고, 그래서 "차저가 많이 오니 빙결탑을 깔자" 같은 계획 자체가 성립하지 않았다.
    /// 이제 숫자키 1~4 또는 건설 메뉴의 타워별 버튼으로 종류를 고르고, 고스트는 고른 종류를
    /// 그대로 보여준다. 설치 후에도 선택이 유지되므로 같은 타워를 연달아 깔 수 있다.
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        public PathData Path;
        public GameManager Game;
        public UIManager UI;
        public Camera Cam;

        public bool MenuOpen { get; private set; }

        private bool _placing;
        private bool _removing;
        private readonly List<GameObject> _towers = new List<GameObject>();
        private GameObject _ghost;
        private SpriteRenderer _ghostSr;
        private Transform _ghostVisual;
        private GameObject _rangeGhost;

        // 철거 모드에서 현재 마우스가 가리키고 있는 타워와, 붉게 칠하기 전의 원래 색.
        private SpriteRenderer _hoverSr;
        private Color _hoverOriginalColor;

        /// <summary>철거할 타워를 고를 때, 클릭 지점에서 이 거리(월드 유닛) 안에 있는 가장 가까운
        /// 타워를 집는다. 타워에 콜라이더가 없으므로 거리 판정으로 대신한다.</summary>
        private const float RemovePickRadius = 0.75f;

        /// <summary>지금 고른 타워 종류. 고스트 미리보기와 실제 설치가 모두 이 값을 쓴다.
        /// UIManager가 건설 메뉴 버튼을 강조할 때도 읽는다.</summary>
        public TowerType SelectedType { get; private set; } = TowerType.Arrow;

        /// <summary>지금 타워를 놓는 중이거나 철거하는 중인지. GameManager가 ESC를 "모드 취소"로
        /// 쓸지 "일시정지"로 쓸지 판단하는 데 쓴다.</summary>
        public bool HasActiveMode => _placing || _removing;

        /// <summary>배치/철거 모드를 밖에서 취소시킨다(ESC 처리, 일시정지 진입 시 사용).</summary>
        public void CancelMode() => CancelSelection();

        private void Update()
        {
            // 일시정지 중에는 건설/철거를 아예 막는다. 그렇지 않으면 시간을 멈춰둔 채 원하는 만큼
            // 타워를 정리할 수 있어서 사실상 무한 계획 시간이 된다.
            // (ESC/P 같은 일시정지 입력은 GameManager.HandlePauseInput에서 따로 처리하므로
            //  여기서 일찍 빠져나가도 일시정지를 풀 수 없게 되지는 않는다.)
            if (Game != null && Game.IsPaused) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame)
            {
                MenuOpen = !MenuOpen;
                if (!MenuOpen) CancelSelection();
                UI.SetBuildMenuOpen(MenuOpen);
            }

            HandleTowerTypeKeys(kb);

            // [해설] ESC는 여기서 처리하지 않는다. "모드 취소"와 "일시정지" 둘 다 ESC를 쓰는데
            // 두 컴포넌트가 같은 프레임에 각자 판정하면 실행 순서에 따라 결과가 달라지므로,
            // 판정을 GameManager 한 곳으로 모으고 필요할 때 CancelMode()를 불러주도록 했다.

            var mouse = Mouse.current;

            if (_placing)
            {
                UpdateGhost();
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                    TryPlace();
                if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                    CancelSelection();
            }
            else if (_removing)
            {
                UpdateRemoveHover();
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                    TryRemove();
                if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                    CancelSelection();
            }
        }

        private bool IsPointerOverUI() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>
        /// 숫자키 1~4로 타워 종류를 고른다. 건설 메뉴가 닫혀 있어도 동작하며, 이 경우 메뉴를
        /// 같이 열어 준다 — 키를 눌렀는데 아무 반응이 없는 것처럼 보이지 않게 하기 위해서다.
        /// [해설] 키 배열을 배열로 만들지 않고 스위치로 쓰는 이유: Input System의 키 참조는
        /// 프로퍼티라 배열을 만들면 매 프레임 할당이 생긴다. 네 개뿐이라 그냥 나열했다.
        /// </summary>
        private void HandleTowerTypeKeys(Keyboard kb)
        {
            // 보상 선택 화면이나 종료 화면처럼 모달 UI가 떠 있을 때는 숫자키를 무시한다
            // (뒤에서 건설 메뉴가 열리고 배치 모드가 켜지는 어색한 상황을 막는다).
            if (Game != null && Game.State != GameState.Prep && Game.State != GameState.Defense) return;

            if (kb.digit1Key.wasPressedThisFrame) SelectTowerType(TowerType.Arrow);
            else if (kb.digit2Key.wasPressedThisFrame) SelectTowerType(TowerType.Ice);
            else if (kb.digit3Key.wasPressedThisFrame) SelectTowerType(TowerType.Lightning);
            else if (kb.digit4Key.wasPressedThisFrame) SelectTowerType(TowerType.Cannon);
        }

        /// <summary>타워 종류를 고르고 곧바로 배치 모드에 들어간다. 건설 메뉴의 타워 버튼과
        /// 숫자키 1~4가 모두 이 경로를 쓴다.</summary>
        public void SelectTowerType(TowerType type)
        {
            SelectedType = type;

            if (!MenuOpen)
            {
                MenuOpen = true;
                UI.SetBuildMenuOpen(true);
            }

            // 이미 배치 중이면 고스트만 새 종류로 갈아끼우고, 아니면 배치 모드를 새로 연다.
            if (_placing && _ghostSr != null && _ghostVisual != null)
                ApplyTowerVisual(_ghostSr, _ghostVisual, SelectedType);
            else
                BeginPlacement();

            UI.RefreshTowerTypeButtons();
        }

        /// <summary>건설 메뉴의 타워 버튼에서 호출한다. 설치될 타입은 지금 고른
        /// SelectedType이며, 고스트가 그 타입을 그대로 보여준다.</summary>
        public void BeginPlacement()
        {
            CancelSelection(); // 철거 모드와 동시에 켜지지 않도록 먼저 정리
            _placing = true;
            EnsureGhost();
        }

        /// <summary>건설 메뉴의 "타워 철거" 버튼에서 호출한다. 철거 모드에 들어가면 마우스를 올린
        /// 타워가 붉게 강조되고 그 타워의 실제 사거리가 함께 보이며, 클릭하면 철거되면서 건설비의
        /// 일부(GameConstants.TowerRefundPercent)를 돌려받는다. 우클릭이나 ESC로 취소한다.</summary>
        public void BeginRemoval()
        {
            CancelSelection(); // 배치 모드와 동시에 켜지지 않도록 먼저 정리
            _removing = true;
            Game.ShowBanner("철거할 타워를 클릭하세요 (우클릭·ESC 취소)");
        }

        private void CancelSelection()
        {
            _placing = false;
            _removing = false;
            ClearRemoveHover();
            DestroyGhostObjects();
        }

        /// <summary>[해설] Unity의 Destroy()는 프레임 끝에야 실제로 파괴하기 때문에, 호출 직후에도
        /// 참조는 한동안 null이 아니다. 아래 EnsureRangeGhost()처럼 "이미 있으면 재사용"하는
        /// 코드가 파괴 예정인 오브젝트를 붙잡는 일이 없도록, 파괴와 동시에 참조를 비워준다.</summary>
        private void DestroyGhostObjects()
        {
            if (_ghost != null) Destroy(_ghost);
            if (_rangeGhost != null) Destroy(_rangeGhost);
            _ghost = null;
            _ghostSr = null;
            _ghostVisual = null;
            _rangeGhost = null;
        }

        private void EnsureGhost()
        {
            DestroyGhostObjects();

            _ghost = new GameObject("PlaceGhost");
            // 실제 타워와 똑같은 자식 구조로 만들어야 미리보기와 결과물의 위치가 일치한다.
            _ghostVisual = new GameObject("Visual").transform;
            _ghostVisual.SetParent(_ghost.transform, false);
            _ghostSr = _ghostVisual.gameObject.AddComponent<SpriteRenderer>();
            _ghostSr.sortingOrder = View.BandGhost + 1;
            ApplyTowerVisual(_ghostSr, _ghostVisual, SelectedType); // 실제 배치될 타워와 동일한 아트/크기로 미리보기

            EnsureRangeGhost();
            _rangeGhost.GetComponent<SpriteRenderer>().color = Color.white;
        }

        /// <summary>사거리 표시용 링을 (없으면) 만든다. 배치 미리보기와 철거 대상 표시가 같은
        /// 오브젝트를 돌려쓴다 — 두 모드는 동시에 켜지지 않기 때문이다.</summary>
        private void EnsureRangeGhost()
        {
            if (_rangeGhost != null)
            {
                _rangeGhost.SetActive(true);
                return;
            }
            _rangeGhost = new GameObject("RangeGhost");
            var rsr = _rangeGhost.AddComponent<SpriteRenderer>();
            rsr.sprite = SpriteFactory.Ring(new Color(1, 1, 1, 0.35f));
            rsr.sortingOrder = View.BandGhost;
        }

        /// <summary>타워 종류의 개수. UIManager가 건설 메뉴 버튼을 몇 개 만들지 정할 때 쓴다.
        /// [해설] Enum.GetValues는 호출할 때마다 배열을 새로 만들기 때문에 static readonly로
        /// 한 번만 계산해 둔다. enum에 값을 추가하면 버튼도 자동으로 따라 늘어난다.</summary>
        public static readonly int TowerTypeCount = System.Enum.GetValues(typeof(TowerType)).Length;

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
            // [해설] 예전에는 여기서 0.35초마다 타입을 다시 굴렸다(무작위 설치). 이제는 고른
            // 종류가 그대로 유지되므로 갱신이 필요 없고, 고스트 아트는 종류를 바꾸는 순간
            // SelectTowerType()에서 한 번만 갈아끼운다.
            Vector3 pos = MouseWorld();
            _ghost.transform.position = pos;
            _rangeGhost.transform.position = pos;

            float range = TowerRangeFor(SelectedType);
            // [해설] 사거리 표시는 바닥에 놓인 원이므로, 누운 평면 위에서는 타원으로 보여야 한다.
            // 다만 실제 사거리 판정(Vector2.Distance)은 눌린 좌표계에서 그대로 하므로 판정 자체는
            // 화면상 정원이다. 그래서 링도 누르지 않고 정원으로 두는 것이 판정과 정확히 일치한다.
            _rangeGhost.transform.localScale = Vector3.one * (range * 2f / 3f); // Ring 스프라이트 지름 3유닛 기준 보정

            bool valid = IsValidPlacement(pos);
            _ghostSr.color = valid ? new Color(1, 1, 1, 0.55f) : new Color(1, 0.3f, 0.3f, 0.55f);
        }

        private float TowerRangeFor(TowerType t) => t switch
        {
            // ※ 각 타워 Setup()의 Range와 반드시 같아야 한다. 2.5D 전환에 맞춰 일괄 하향했다
            //   (ArrowTower.Setup의 해설 참고).
            TowerType.Arrow => 2.6f,
            TowerType.Ice => 2.1f,
            TowerType.Cannon => 2.7f,
            TowerType.Lightning => 2.45f,
            _ => 2.5f
        };

        private bool IsValidPlacement(Vector3 pos)
        {
            if (Mathf.Abs(pos.x) > GameConstants.WorldHalfWidth - 0.3f) return false;
            if (Mathf.Abs(pos.y) > GameConstants.WorldHalfHeight - 0.3f) return false;

            // [해설] ★ 타워는 길(사각형 루프) <b>안쪽</b>에만 세울 수 있다. 예전에는 이 검사가
            // 없어서 사각형 바깥 빈 땅 어디에나 세워졌고, 화면 전체에 타워가 흩어지는 그림이
            // 나왔다. 아래 거리 검사(길에 너무 붙지 않기)와 합쳐지면, 실제 건설 가능 영역은
            // "사각형을 MinDistanceFromPath만큼 안으로 줄인 정사각형"이 된다.
            if (!Path.ContainsPoint(pos)) return false;
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

            // [해설] 설치되는 타입은 지금 고스트가 보여주고 있던 타입 그대로 확정한다(WYSIWYG).
            TowerType type = SelectedType;
            int cost = GameConstants.CostFor(type);
            if (Game.Gold < cost)
            {
                Game.ShowBanner("골드가 부족합니다");
                return;
            }

            Game.SpendGold(cost);
            SpawnTower(type, pos);
            Game.ShowBanner($"{TowerLabel(type)} 설치!");

            // [해설] 설치 후에도 고른 종류를 그대로 유지한다 — 같은 타워를 여러 개 깔 때
            // 매번 다시 고르지 않아도 되도록. 고스트 아트도 바뀌지 않으므로 다시 그릴 필요가 없다.
        }

        // ---------- 타워 철거 ----------

        /// <summary>클릭 지점에서 RemovePickRadius 안에 있는 가장 가까운 타워를 돌려준다(없으면 null).</summary>
        private GameObject FindTowerAt(Vector3 pos)
        {
            GameObject best = null;
            float bestDist = RemovePickRadius;
            foreach (var t in _towers)
            {
                if (t == null) continue;
                // [해설] ★ 2.5D 전환의 부작용 수정. 타워 본체는 바닥 지점에 있고 그림은 그보다
                // 위에 그려지므로, 바닥 지점으로만 거리를 재면 <b>눈에 보이는 탑 몸통을 클릭해도
                // 안 잡히고</b> 발밑을 정확히 찍어야 하는 이상한 조작이 된다. 그림의 한가운데를
                // 기준으로 재서, 보이는 대로 클릭하면 잡히게 한다.
                Vector3 body = t.transform.position + new Vector3(0f, TowerArtWorldHeight * 0.5f, 0f);
                float d = Vector2.Distance(body, pos);
                if (d >= bestDist) continue;
                bestDist = d;
                best = t;
            }
            return best;
        }

        /// <summary>철거 모드에서 마우스가 가리키는 타워를 붉게 강조하고 사거리를 보여준다.</summary>
        private void UpdateRemoveHover()
        {
            GameObject hit = IsPointerOverUI() ? null : FindTowerAt(MouseWorld());
            // 스프라이트는 타워 본체가 아니라 자식("Visual")에 있다(2.5D 발밑 정렬).
            var sr = hit != null ? hit.GetComponentInChildren<SpriteRenderer>() : null;
            if (sr == _hoverSr) return; // 가리키는 대상이 그대로면 아무것도 하지 않는다

            ClearRemoveHover();
            if (hit == null || sr == null) return;

            _hoverSr = sr;
            _hoverOriginalColor = sr.color;
            sr.color = new Color(1f, 0.45f, 0.45f, 1f);

            EnsureRangeGhost();
            var tb = hit.GetComponent<TowerBase>();
            float range = tb != null ? tb.Range : 2.6f;
            _rangeGhost.transform.position = hit.transform.position;
            _rangeGhost.transform.localScale = Vector3.one * (range * 2f / 3f); // Ring 스프라이트 지름 3유닛 기준 보정
            _rangeGhost.GetComponent<SpriteRenderer>().color = new Color(1f, 0.5f, 0.5f, 1f);
        }

        /// <summary>강조해 둔 타워의 색을 원래대로 돌려놓고 사거리 링을 숨긴다.</summary>
        private void ClearRemoveHover()
        {
            if (_hoverSr != null)
            {
                // 호버 시작 시점에 찍어둔 색이 아니라 타워의 기본색으로 되돌린다(위 SpawnTower 해설 참고).
                var hoverTb = _hoverSr.GetComponentInParent<TowerBase>();
                _hoverSr.color = hoverTb != null ? hoverTb.BaseColor : _hoverOriginalColor;
            }
            _hoverSr = null;
            if (_rangeGhost != null && !_placing) _rangeGhost.SetActive(false);
        }

        private void TryRemove()
        {
            var target = FindTowerAt(MouseWorld());
            if (target == null)
            {
                Game.ShowBanner("철거할 타워를 클릭하세요");
                return;
            }

            var tb = target.GetComponent<TowerBase>();
            TowerType type = tb != null ? tb.Type : TowerType.Arrow;
            int refund = GameConstants.RefundFor(type);

            ClearRemoveHover(); // 곧 파괴될 타워를 가리키고 있던 상태를 먼저 정리한다
            _towers.Remove(target);
            Destroy(target); // TowerBase.OnDisable이 Active 목록에서도 자동으로 빠진다

            Game.RefundGold(refund);
            Game.ShowBanner($"{TowerLabel(type)} 철거 — 골드 {refund} 반환");
            // 연달아 여러 개를 철거할 수 있도록 철거 모드는 그대로 유지한다.
        }

        /// <summary>화면에 보여줄 타워 이름. 건설 메뉴 버튼도 이 이름을 쓰므로 public이다.</summary>
        public static string TowerLabel(TowerType t) => t switch
        {
            TowerType.Arrow => "화살탑",
            TowerType.Ice => "빙결탑",
            TowerType.Cannon => "포격탑",
            TowerType.Lightning => "번개탑",
            _ => "타워"
        };

        private const float TowerArtWorldHeight = 1.5f;

        /// <summary>
        /// 실제로 설치되는 타워와, 배치 전 미리보기(고스트)가 항상 같은 아트/크기를 쓰도록
        /// 스프라이트 지정 로직을 한 곳에 모았다. Resources/Sprites/Tower_Arrow.png,
        /// Tower_Ice.png, Tower_Cannon.png, Tower_Lightning.png가 있으면 그 아트를 쓰고, 없으면 도형으로 대체한다.
        /// </summary>
        /// <summary>
        /// [해설] ★ 2.5D 전환. t는 이제 타워 본체가 아니라 <b>그림만 담는 자식</b>이다. 스케일을
        /// 걸고 나서 몸 높이의 절반만큼 위로 올려, 스프라이트 아래쪽 끝이 바닥 지점에 닿게 한다.
        /// 본체는 바닥 지점에 그대로 있으므로 사거리·배치 판정은 전혀 바뀌지 않는다.
        /// </summary>
        private void ApplyTowerVisual(SpriteRenderer sr, Transform t, TowerType type)
        {
            Sprite art = Resources.Load<Sprite>($"Sprites/Tower_{type}");
            if (art != null)
            {
                sr.sprite = art;
                sr.color = Color.white;
                float scale = TowerArtWorldHeight / art.bounds.size.y;
                t.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                Color fill = type switch
                {
                    TowerType.Arrow => new Color(0.24f, 0.4f, 0.78f),
                    TowerType.Ice => new Color(0.35f, 0.85f, 0.92f),
                    TowerType.Cannon => new Color(0.95f, 0.58f, 0.28f),
                    TowerType.Lightning => new Color(0.98f, 0.88f, 0.3f), // 번개 = 노랑
                    _ => Color.white
                };
                // [해설] ★ 크기 버그 수정. 예전에는 여기서 localScale을 1로 두었는데, Triangle은
                // 64px/PPU 32 = 2.0유닛이라 아트가 있는 타워(TowerArtWorldHeight = 1.5유닛)보다
                // 33% 크게 그려졌다. 아트가 아직 없는 번개탑만 유독 거대해 보이고 배치 고스트의
                // 크기도 다른 타워와 달라지므로, 대체 도형도 같은 높이로 맞춘다.
                var shape = SpriteFactory.Triangle(fill, Color.white);
                sr.sprite = shape;
                float shapeScale = TowerArtWorldHeight / shape.bounds.size.y;
                t.localScale = new Vector3(shapeScale, shapeScale, 1f);
            }

            // 발밑 기준으로 올린다. 어떤 아트/도형이 와도 최종 높이는 TowerArtWorldHeight이므로
            // 올리는 양도 항상 그 절반이다.
            t.localPosition = new Vector3(0f, TowerArtWorldHeight * 0.5f, 0f);
        }

        private void SpawnTower(TowerType type, Vector3 pos)
        {
            var go = new GameObject($"Tower_{type}");
            go.transform.position = pos;

            // 그림은 자식에 둔다(2.5D 발밑 정렬 — ApplyTowerVisual 해설 참고).
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            var sr = visual.gameObject.AddComponent<SpriteRenderer>();
            // 타워와 적을 같은 밴드에 두어, 적이 타워 앞을 지나가면 앞에 그려지게 한다.
            sr.sortingOrder = View.Order(View.BandActor, pos.y);
            ApplyTowerVisual(sr, visual, type);

            switch (type)
            {
                case TowerType.Arrow: go.AddComponent<ArrowTower>().Setup(); break;
                case TowerType.Ice: go.AddComponent<IceTower>().Setup(); break;
                case TowerType.Cannon: go.AddComponent<CannonTower>().Setup(); break;
                case TowerType.Lightning: go.AddComponent<LightningTower>().Setup(); break;
            }

            // [해설] ★ 색 복원 버그 수정. 철거 호버(빨강)와 보스의 무력화(회색)가 겹치면,
            // 나중에 끝난 쪽이 "자기가 시작할 때 본 색"을 되돌려 써서 타워가 영구히 빨갛거나
            // 회색으로 굳는 일이 있었다. 이제 원래 색의 출처를 TowerBase.BaseColor 하나로
            // 정하고, 강조/무력화가 끝나면 둘 다 무조건 이 색으로 되돌린다.
            var placed = go.GetComponent<TowerBase>();
            if (placed != null) placed.BaseColor = sr.color;

            _towers.Add(go);
        }

        /// <summary>
        /// 스테이지가 바뀔 때 설치된 타워를 <b>전부</b> 철거하고 건설비를 전액 돌려준다.
        ///
        /// [해설] 스테이지마다 길(사각형 루프)의 위치와 크기가 통째로 달라진다. 처음에는 새 길
        /// 기준으로 무효가 된 타워만 골라 지웠는데, 그러면 살아남은 타워와 새로 지은 타워가
        /// 뒤섞여 배치가 누더기가 되고 "왜 저건 사라지고 저건 남았지?"를 플레이어가 알 수 없다.
        /// 판을 통째로 비우면 규칙이 "스테이지가 바뀌면 처음부터 다시 짠다" 한 줄로 끝나고,
        /// 스테이지 전환이 분명한 분기점으로 읽힌다.
        ///
        /// 환불을 50%(철거와 동일)가 아니라 전액으로 하는 이유: 플레이어가 잘못 지은 게 아니라
        /// 게임 쪽 사정으로 부수는 것이라, 손해를 지우면 스테이지 전환이 그냥 벌점이 된다.
        /// 전액을 돌려주므로 새 길에서 같은 규모로 다시 지을 수 있다.
        /// </summary>
        public void ClearAllTowersForNewStage()
        {
            _towers.RemoveAll(t => t == null);
            if (_towers.Count == 0) return;

            int refunded = 0;
            foreach (var go in _towers)
            {
                var tb = go.GetComponent<TowerBase>();
                refunded += GameConstants.CostFor(tb != null ? tb.Type : TowerType.Arrow);
                Destroy(go); // TowerBase.OnDisable이 Active 목록에서도 빼 준다
            }
            int removed = _towers.Count;
            _towers.Clear();

            CancelMode();       // 배치/철거 중이었다면 모드도 닫는다
            ClearRemoveHover(); // 방금 파괴된 타워를 가리키고 있었을 수 있다

            // [해설] 배너는 호출부(GameManager.OnWaveClearedHandler)가 스테이지 클리어 소식과
            // 합쳐서 하나만 띄운다. 여기서 또 띄우면 그 배너를 같은 프레임에 덮어 버린다.
            // 대신 환불 금액은 알려줘야 하므로 골드 표시만 갱신한다.
            Game.RefundGold(refunded);
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
