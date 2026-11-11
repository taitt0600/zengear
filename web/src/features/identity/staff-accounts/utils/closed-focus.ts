/**
 * The `onCloseAutoFocus` of the dialogs of SCR-STF-03, which open without a trigger, on that page and from a row of
 * SCR-STF-01. Radix would focus the dialog's own trigger, which it has none of, and the focus would fall to the page's
 * body: `onClosed` puts it back itself (as `reauth-dialog.tsx` does).
 */
export function closedFocus(onClosed: () => void): (event: Event) => void {
  return (event) => {
    event.preventDefault()
    onClosed()
  }
}
