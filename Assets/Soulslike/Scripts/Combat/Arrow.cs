using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// 화살 투사체다. 물리(Rigidbody) 없이 매 프레임 이동 구간을 레이캐스트로 검사한다.
    /// 빠른 화살이 얇은 콜라이더를 뚫고 지나가는 문제(터널링)를 막으려는 것이다.
    /// 맞으면 IDamageable에 피해를 주고, 맞은 자리에 박힌 채 남았다가 사라진다.
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        [Tooltip("박힐 때 화살촉이 물체 안으로 들어가는 깊이(m). 나머지 화살대는 밖에 남는다.")]
        [SerializeField] float stickPenetration = 0.12f;

        readonly RaycastHit[] _hits = new RaycastHit[8];
        // 이 오브젝트 원점에서 화살촉 끝까지의 앞쪽 길이(m). 모델 원점이 꼬리(오늬)에 있으면 화살 길이만큼이 된다.
        float _tipDistance;

        Vector3 _velocity;
        float _gravity;
        float _damage;
        GameObject _owner;
        LayerMask _mask;
        float _life;
        bool _stuck;

        public bool HasHit => _stuck;
        /// <summary>맞힌 대상(IDamageable이 아니었으면 null).</summary>
        public IDamageable HitTarget { get; private set; }
        public Vector3 Velocity => _velocity;

        public void Launch(Vector3 velocity, float damage, GameObject owner, float gravity, float lifetime, LayerMask mask)
        {
            _velocity = velocity;
            _damage = damage;
            _owner = owner;
            _gravity = gravity;
            _life = lifetime;
            _mask = mask;
            if (velocity.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(velocity);
            _tipDistance = MeasureTipDistance();
        }

        /// <summary>자식 모델 메시의 꼭짓점 범위 중 앞쪽(+Z)으로 가장 먼 거리. 모델이 없으면 0.</summary>
        float MeasureTipDistance()
        {
            float tip = 0f;
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                Transform t = filter.transform;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    float along = Vector3.Dot(t.TransformPoint(corner) - origin, forward);
                    if (along > tip) tip = along;
                }
            }
            return tip;
        }

        void Update()
        {
            if (_stuck) return;

            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 step = _velocity * dt;
            float distance = step.magnitude;
            if (distance > 0f && TryFindHit(transform.position, step / distance, distance, out RaycastHit hit))
            {
                OnHit(hit, step / distance);
                return;
            }

            transform.position += step;
            _velocity += Vector3.down * (_gravity * dt);
            if (_velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(_velocity);
        }

        bool TryFindHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, _mask, QueryTriggerInteraction.Ignore);
            nearest = default;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                // 쏜 사람(플레이어)과 그 장비에는 맞지 않는다.
                if (_owner != null && _hits[i].transform.IsChildOf(_owner.transform)) continue;
                if (_hits[i].distance < best)
                {
                    best = _hits[i].distance;
                    nearest = _hits[i];
                }
            }
            return best < float.MaxValue;
        }

        void OnHit(RaycastHit hit, Vector3 direction)
        {
            _stuck = true;
            // 원점을 맞은 점에 두면 원점보다 앞에 있는 화살대 전체가 물체 안에 묻혀 사라진다.
            // 화살촉만 조금 들어가도록 화살 길이만큼 뒤로 물린다.
            float back = Mathf.Max(0f, _tipDistance - stickPenetration);
            transform.position = hit.point - direction * back;

            HitTarget = hit.collider.GetComponentInParent<IDamageable>();
            HitTarget?.TakeDamage(new DamageInfo(_damage, _owner, hit.point, direction));

            // 맞은 물체를 따라 움직이도록 박아 두고, 잠시 뒤 지운다.
            transform.SetParent(hit.collider.transform, true);
            Destroy(gameObject, 10f);
        }
    }
}
