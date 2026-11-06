/**
 * The `onCloseAutoFocus` of a dialog of SCR-STF-03 that a row of SCR-STF-01 opens. Opened without a trigger, Radix
 * would focus the dialog's own trigger, which it has none of, and the focus would fall to the page's body: `onClosed`
 * puts it back itself (as `reauth-dialog.tsx` does). Without `onClosed`, Radix's default stays.
 */
export function closedFocus(onClosed?: () => void): ((event: Event) => void) | undefined {
  if (!onClosed) return undefined
  return (event) => {
    event.preventDefault()
    onClosed()
  }
}
