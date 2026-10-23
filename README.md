# APCenter
택배 물류 센터 게임입니다

## 애플리케이션 구조
Assets/
│
├── _Project/
│   │
│   ├── Scenes/
│   │   │
│   │   ├── SampleScene1/
│   │   │   ├── SampleScene1.unity
│   │   │   │
│   │   │   ├── Art/
│   │   │   │   ├── Models/           # 3D 모델
│   │   │   │   ├── Materials/        # 머티리얼
│   │   │   │   ├── Textures/         # 텍스처
│   │   │   │   ├── Animations/       # 애니메이션 클립
│   │   │   │   └── VFX/              # 이펙트
│   │   │   │
│   │   │   ├── Audio/
│   │   │   │   ├── BGM/
│   │   │   │   └── SFX/
│   │   │   │
│   │   │   ├── Prefabs/
│   │   │   │   ├── Characters/
│   │   │   │   ├── Environment/
│   │   │   │   ├── UI/
│   │   │   │   └── Effects/
│   │   │   │
│   │   │   ├── Scripts/
│   │   │   ├── ScriptableObjects/
│   │   │   └── Settings/
│   │   │
│   │   └── SampleScene2/
│   │       ├── SampleScene2.unity
│   │       ├── Art/
│   │       ├── Audio/
│   │       ├── Prefabs/
│   │       ├── Scripts/
│   │       ├── ScriptableObjects/
│   │       └── Settings/
│   │
│   ├── Common/                  # 여러 씬이 공유하는 리소스
│   │   ├── Art/
│   │   ├── Audio/
│   │   ├── Prefabs/
│   │   ├── Scripts/
│   │   └── ScriptableObjects/
│   │
│   ├── Core/                    # 게임 전체 시스템
│   │   ├── GameManager/
│   │   ├── SceneManagement/
│   │   ├── SaveLoad/
│   │   ├── ObjectPooling/
│   │   └── Input/
│   │
│   ├── Settings/                # 프로젝트 공통 설정
│   ├── Editor/                  # 에디터 전용 도구
│   └── Tests/                   # 테스트 코드
│       ├── EditMode/
│       └── PlayMode/
│
├── Plugins/                     # 외부 플러그인
└── ThirdParty/                  # 외부 에셋

## **이슈 / PR 제목**

```
[Type] Content
```

| **Type** | **설명** |
| --- | --- |
| `Feat` | 새로운 기능 |
| `Fix` | 버그 수정 |
| `Docs` | 문서 작업 |
| `Style` | 코드 스타일 변경 |
| `Refactor` | 리팩토링 |
| `Test` | 테스트 관련 |
| `Chore` | 기타 작업 |
| `Art` | 원화 및 모델링 추가 |
| `Sound` | SFX, BGM 추가 |

**예시**

```
[Feat] 플레이어 이동 속도 조절 기능 추가
[Fix] 메테리얼 적용 오류 수정
[Docs] README 컨벤션 항목 추가
[Art] 컨베이어 벨트 모델링 추가
[Sound] 레벨업 SFX 추가
```

# **커밋 컨벤션**

Conventional Commits 규칙을 따릅니다.

```
<type>: <subject>
```

- **type**: 영어로 작성
- **subject**: 한글로 작성 (50자 이내)

| **Type** | **설명** |
| --- | --- |
| `feat` | 새로운 기능 추가 |
| `fix` | 버그 수정 |
| `docs` | 문서 변경 |
| `style` | 코드 포맷, 공백 정리 (기능 변화 없음) |
| `refactor` | 리팩토링 (기능 추가 및 버그 수정 없음) |
| `test` | 테스트 추가 또는 수정 |
| `chore` | 빌드 설정, 패키지 관리 등 기타 변경 |
| `perf` | 성능 개선 |
| `art` | 원화 및 모델링 추가 |
| `sound`  | sfx 및 bgm 추가 |

**예시**

```
feat: 이동 속도 조절 기능 추가
fix: 체력바 표시 오류 수정
docs: README 컨벤션 항목 추가
art : 메인 화면 이미지 추가
sound : 이벤트 sfx 추가
```

# Art팀 전달사항
GitHub Desktop 사용하여 파일 업로드

이슈 발생 시 반드시 프로그래밍 팀에 전달할 것
