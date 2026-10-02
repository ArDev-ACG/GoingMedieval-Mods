# Iconos

`actuales/` es lo que el juego carga hoy, tal cual. `actuales/vista/` es lo
mismo a 4x sobre un damero para poder mirarlo; esa carpeta es solo para ver,
de ahi no se instala nada.

Para cambiar uno: deja el PNG en `nuevos/` - suelto o dentro de la carpeta de
su mod, da igual - **con el mismo nombre de fichero** y corre:

    python tools/icons/collect_icons.py --install

El nombre es lo unico que importa: `ModInstance.LoadSprites()` registra cada
fichero bajo su propio nombre, asi que el nombre *es* el `iconPath` que dice
el JSON. Un nombre mal escrito no da error: el sprite simplemente no aparece.

Tamano: los iconos de vanilla son de 128 px y opacos. Cualquier tamano carga,
pero el juego lo escala a la casilla de la UI.

## Lo que hay

**CarrionAndPlague**

- `aldrich_bubble_plague_bite.png`
- `aldrich_bubble_plague_immune.png`
- `aldrich_icon_cat_statue.png`
- `aldrich_icon_plague_hood.png`
- `aldrich_icon_plague_mask.png`

**Gravedigger**

- `aldrich_bubble_grave_duty.png`
- `aldrich_bubble_grave_stripped.png`
- `aldrich_icon_mass_grave.png`
- `aldrich_icon_mass_pyre.png`
- `aldrich_perk_gravedigger.png`
- `aldrich_role_gravedigger.png`

**UndeadHorde**

- `aldrich_icon_undead_claws.png`
- `aldrich_perk_risen.png`

**VampireCourt**

- `aldrich_bubble_bite_mark.png`
- `aldrich_bubble_bite_missed.png`
- `aldrich_bubble_blood_drained.png`
- `aldrich_bubble_blood_feast.png`
- `aldrich_bubble_count_duty.png`
- `aldrich_bubble_count_stripped.png`
- `aldrich_icon_blood_altar.png`
- `aldrich_icon_blood_brazier.png`
- `aldrich_icon_blood_circle.png`
- `aldrich_icon_blood_draught.png`
- `aldrich_icon_blood_well.png`
- `aldrich_icon_count_coffin.png`
- `aldrich_icon_count_crypt.png`
- `aldrich_icon_count_throne.png`
- `aldrich_icon_court_banner.png`
- `aldrich_icon_court_banner_wall.png`
- `aldrich_icon_court_reliquary.png`
- `aldrich_icon_crimson_candle.png`
- `aldrich_icon_impaled_stake.png`
- `aldrich_icon_veiled_mirror.png`
- `aldrich_icon_vigil_table.png`
- `aldrich_perk_ghoul.png`
- `aldrich_perk_thrall.png`
- `aldrich_perk_vampire.png`
- `aldrich_role_count.png`
