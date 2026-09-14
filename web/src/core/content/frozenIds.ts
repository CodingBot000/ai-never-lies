/** 고정 ID 집합 — 추출 검증용. 02_CODE_GENERATION_INSTRUCTIONS 15장과 동일하다. */

export const FrozenIds = {
  Scenes: [
    's000', 's010', 's020',
    's100', 's110', 's120', 's130', 's140', 's150', 's160', 's170',
    's200', 's210', 's220', 's230', 's240',
    's300', 's310', 's320', 's330', 's340',
    's400', 's410', 's411', 's420', 's430', 's440', 's450', 's460',
    's500', 's510', 's520', 's530', 's540', 's550',
    's600', 's610', 's620', 's630', 's640', 's650',
    's700', 's710',
  ],
  Evidence: [
    'ev01', 'ev02', 'ev03', 'ev04', 'ev05', 'ev06', 'ev07', 'ev08', 'ev09', 'ev10', 'ev11', 'ev12',
    'ev13', 'ev14', 'ev15', 'ev16', 'ev17', 'ev18', 'ev19', 'ev20', 'ev21', 'ev22', 'ev23', 'ev24',
    'ev25', 'ev26', 'ev27', 'ev28', 'ev29', 'ev30', 'ev31', 'ev32', 'ev33', 'ev34', 'ev35', 'ev36',
  ],
  Puzzles: [
    'p01', 'p02', 'p03', 'p04', 'p05', 'p06', 'p07', 'p08', 'p09', 'p10',
    'p11', 'p12', 'p13', 'p14', 'p15', 'p16', 'p17', 'p18', 'p19', 'p20',
  ],
} as const

export const CONTENT_FILES = [
  'episode.json',
  'scenes.json',
  'dialogues.ko.json',
  'evidence.json',
  'testimonies.json',
  'branches.json',
  'puzzles.json',
  'hints.ko.json',
  'ui.ko.json',
  'graph.json',
  'matrix.json',
] as const

export type ContentFileName = (typeof CONTENT_FILES)[number]
