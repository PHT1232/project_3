import AuthPlaceholder from '../components/AuthPlaceholder.jsx'

/**
 * SCOPE FLAG: self-registration is not in the approved Plan. Employees are created by
 * Manager+ via `POST /api/v1/users` (Plan §4.2, DFD L2 2.3), the requirements document says
 * "There are registered people in the system", and sign-up is not among the 12 open `[ASK]`
 * questions. This route exists because it was requested; confirm it before anyone builds it.
 */
export default function SignUp() {
  return (
    <AuthPlaceholder
      title="Sign Up"
      note="Not specified in the Plan — employees are created by Manager+ via POST /api/v1/users. Confirm scope before implementing."
    />
  )
}
