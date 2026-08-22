import { gregorianToJalali, jalaliToGregorian, jalaliMonthLength, isLeapJalaliYear, isoToJalaliText, jalaliTextToIso } from './src/lib/jalali'

const cases: [string, string][] = [
  ['2026-08-22', '1405/05/31'],
  ['1979-02-11', '1357/11/22'],
  ['2000-01-01', '1378/10/11'],
  ['2024-03-20', '1403/01/01'],
  ['2025-03-21', '1404/01/01'],
  ['2026-03-21', '1405/01/01'],
  ['1990-06-15', '1369/03/25'],
]
let bad = 0
for (const [iso, expected] of cases) {
  const got = isoToJalaliText(iso)
  const back = jalaliTextToIso(got)
  const ok = got === expected && back === iso
  if (!ok) bad++
  console.log((ok ? 'OK  ' : 'FAIL') + ` ${iso} -> ${got} (expected ${expected}) -> back ${back}`)
}

// رفت‌وبرگشت روی ۲۰ سال، هر روز
let rt = 0, rtBad = 0
for (let jy = 1395; jy <= 1414; jy++) {
  for (let jm = 1; jm <= 12; jm++) {
    for (let jd = 1; jd <= jalaliMonthLength(jy, jm); jd++) {
      const g = jalaliToGregorian(jy, jm, jd)
      const j = gregorianToJalali(g.gy, g.gm, g.gd)
      rt++
      if (j.jy !== jy || j.jm !== jm || j.jd !== jd) { rtBad++; if (rtBad < 4) console.log('RT FAIL', jy, jm, jd, '->', j) }
    }
  }
}
console.log(`round-trip: ${rt} dates, ${rtBad} failures`)
console.log('leap years 1395-1414:', Array.from({length:20},(_,i)=>1395+i).filter(isLeapJalaliYear).join(', '))
console.log(bad === 0 && rtBad === 0 ? 'ALL PASS' : 'FAILURES PRESENT')
