# G1 Art Preflight

- 결과: PASS
- 검사 시각(UTC): 2026-09-14T17:18:23Z
- 아트 자산: 54/54
- 이슈: 0

| 검사 | 결과 | 상세 |
| --- | --- | --- |
| ready.ready | PASS | ready must be true |
| ready.contractVersion | PASS | art.v1 required |
| ready.episodeId | PASS | ep01 required |
| ready.counts | PASS | required=54 approved=54 |
| ready.blockers | PASS | blockers must be empty |
| doc.masterDocumentSha256 | PASS | expected 00790af9733c81b7dbc2b3dfcb0c913a27aa1d82880276f8ff303e05bd7d1dce actual 00790af9733c81b7dbc2b3dfcb0c913a27aa1d82880276f8ff303e05bd7d1dce |
| doc.artInstructionSha256 | PASS | expected 6cc43d420c3d00c8d58987f5c57a780d54d0f251fa61c0dc7cc355b38ec435af actual 6cc43d420c3d00c8d58987f5c57a780d54d0f251fa61c0dc7cc355b38ec435af |
| doc.codeInstructionSha256 | PASS | expected 8fcd66f7dc11cb24100f6d315e946501bcd90d7bf12016018faa6f3c83ee04e1 actual 8fcd66f7dc11cb24100f6d315e946501bcd90d7bf12016018faa6f3c83ee04e1 |
| manifest.sha256 | PASS | expected 8d1e4a63829632972187bac6e9538a161dee0b72785859058d5e57ecc1968bc3 actual 8d1e4a63829632972187bac6e9538a161dee0b72785859058d5e57ecc1968bc3 |
| lock.count | PASS | count=54 unique=54 |
| plan.count | PASS | count=54 unique=54 |
| id-set.lock | PASS | missing=[] extra=[] |
| id-set.plan | PASS | missing=[] extra=[] |
| path-set | PASS | plan and lock paths must match |
| path.lowercase | PASS | non-lowercase paths: [] |
| runtime.png.count | PASS | actual=54 |
| runtime.png.set | PASS | missing=[] extra=[] |
